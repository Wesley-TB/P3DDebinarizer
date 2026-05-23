using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace P3DDebin.Core;

public static class Converter
{
    private static readonly Assembly _bis;
    private static readonly ConstructorInfo _odolCtorPath;
    private static readonly ConstructorInfo _odolCtorStream;
    private static readonly MethodInfo _odol2mlod;
    private static readonly MethodInfo _writeToFile;

    static Converter()
    {
        // Em single-file publish o Assembly.Location e vazio; usamos
        // AppContext.BaseDirectory como pasta da aplicacao. Quando o BisDll esta
        // bundled (build self-contained com IncludeAllContentForSelfExtract),
        // ele e resolvido pelo loader padrao via Assembly.Load.
        string baseDir = AppContext.BaseDirectory;
        string dllPath = Path.Combine(baseDir, "BisDll.dll");

        if (File.Exists(dllPath))
            _bis = Assembly.LoadFrom(dllPath);
        else
            _bis = Assembly.Load("BisDll");

        var odolType = _bis.GetType("BisDll.Model.ODOL.ODOL")
                       ?? throw new InvalidOperationException("BisDll.Model.ODOL.ODOL not found");
        _odolCtorPath = odolType.GetConstructor(new[] { typeof(string) })!;
        _odolCtorStream = odolType.GetConstructor(new[] { typeof(Stream) })!;

        var convType = _bis.GetType("BisDll.Model.Conversion")
                       ?? throw new InvalidOperationException("BisDll.Model.Conversion not found");
        _odol2mlod = convType.GetMethod("ODOL2MLOD")
                     ?? throw new InvalidOperationException("Conversion.ODOL2MLOD not found");

        var mlodType = _bis.GetType("BisDll.Model.MLOD.MLOD")
                       ?? throw new InvalidOperationException("BisDll.Model.MLOD.MLOD not found");
        _writeToFile = mlodType.GetMethod("writeToFile", new[] { typeof(string), typeof(bool) })!;
    }

    public static P3DFormat DetectFormat(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] magic = new byte[4];
            if (fs.Read(magic, 0, 4) < 4)
                return P3DFormat.Unknown;

            string m = System.Text.Encoding.ASCII.GetString(magic);
            return m switch
            {
                "ODOL" => P3DFormat.Odol,
                "MLOD" => P3DFormat.Mlod,
                _      => P3DFormat.Unknown
            };
        }
        catch
        {
            return P3DFormat.Unknown;
        }
    }

    // Le apenas as tabelas de endereco de LOD de um ODOL (sem patches),
    // necessarias para corrigir os offsets ao trocar caminhos in-place.
    public static bool TryReadOdolAddresses(string path, out uint[] start, out uint[] end)
    {
        start = Array.Empty<uint>();
        end   = Array.Empty<uint>();

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        try
        {
            object odol = _odolCtorPath.Invoke(new object[] { path })!;
            Type t = odol.GetType();

            start = (uint[])t.GetField("lodStartAdresses", flags)!.GetValue(odol)!;
            end   = (uint[])t.GetField("lodEndAdresses", flags)!.GetValue(odol)!;

            return start.Length > 0 && start.Length == end.Length;
        }
        catch
        {
            return false;
        }
    }

    public static object? LoadOdol(string inputPath, Action<string>? log = null)
    {
        void Log(string msg) => log?.Invoke(msg);

        uint version = ReadVersion(inputPath);
        Log(Strings.T("Core.LogOdolVersion", version, _bis.GetName().Version!));
        return TryLoadOdolWithPatches(inputPath, version, Log);
    }

    public static ConversionResult Convert(string inputPath, string outputFolder, Action<string>? log = null)
    {
        void Log(string msg) => log?.Invoke(msg);

        string outputPath = BuildOutputPath(inputPath, outputFolder, Log);

        try
        {
            uint version = ReadVersion(inputPath);
            Log(Strings.T("Core.LogOdolVersion", version, _bis.GetName().Version!));

            object? odol = TryLoadOdolWithPatches(inputPath, version, Log);

            if (odol == null && LooksProtectedOdol(inputPath))
                Log(Strings.T("Core.LogProtectedOdol"));

            if (odol == null)
            {
                if (LooksProtectedOdol(inputPath))
                    return Fail(inputPath, outputPath, Strings.T("Core.ResultProtectedOdol", version));

                return Fail(inputPath, outputPath, Strings.T("Core.ResultCannotLoadOdol", version));
            }

            Log(Strings.T("Core.LogConvertingOdol"));
            object? mlod = _odol2mlod.Invoke(null, new[] { odol });
            if (mlod == null)
                return Fail(inputPath, outputPath, Strings.T("Core.ResultOdol2MlodNull"));

            _writeToFile.Invoke(mlod, new object[] { outputPath, true });

            return Ok(inputPath, outputPath, Strings.T("Core.ResultOdolOk", version));
        }
        catch (Exception ex)
        {
            return Fail(inputPath, outputPath, Strings.T("Core.ResultError", Unwrap(ex)));
        }
    }

    private static object? TryLoadPath(string path, Action<string> log)
    {
        try
        {
            return _odolCtorPath.Invoke(new object[] { path });
        }
        catch (Exception ex)
        {
            log(Strings.T("Core.LogLoadDirectFailed", Unwrap(ex)));
            return null;
        }
    }

    private static object? TryLoadStream(Stream stream, Action<string> log)
    {
        try
        {
            return _odolCtorStream.Invoke(new object[] { stream });
        }
        catch (Exception ex)
        {
            log(Strings.T("Core.LogLoadStreamFailed", Unwrap(ex)));
            return null;
        }
    }

    private static object? TryLoadOdolWithPatches(string path, uint version, Action<string> log)
    {
        object? odol = TryLoadPath(path, log);

        if (odol == null && version >= 74)
        {
            log(Strings.T("Core.LogTryFixHeader"));
            using var reordered = ReorderV75Header(path, log);
            if (reordered != null)
                odol = TryLoadStream(reordered, log);
        }

        if (odol == null && version >= 75)
        {
            log(Strings.T("Core.LogApplyHeaderPatch"));
            using var patched = PatchV75(path, log);
            if (patched != null)
                odol = TryLoadStream(patched, log);
        }

        return odol;
    }

    private static MemoryStream? ReorderV75Header(string path, Action<string> log)
    {
        byte[] orig = File.ReadAllBytes(path);
        if (orig.Length < 32)
            return null;

        uint extraA = BitConverter.ToUInt32(orig, 12);
        uint extraB = BitConverter.ToUInt32(orig, 16);

        int stringStart = 20;
        int stringEnd = stringStart;
        while (stringEnd < orig.Length && orig[stringEnd] != 0)
            stringEnd++;

        if (stringEnd >= orig.Length)
            return null;

        int stringLen = stringEnd - stringStart + 1;
        if (stringLen <= 1)
            return null;

        bool looksLikePath =
            orig[stringStart] == (byte)'\\' ||
            orig[stringStart] == (byte)'/' ||
            orig[stringStart] is >= 32 and <= 126;

        if (!looksLikePath)
            return null;

        byte[] patched = new byte[orig.Length];
        Buffer.BlockCopy(orig, 0, patched, 0, 12);
        Buffer.BlockCopy(orig, stringStart, patched, 12, stringLen);
        Buffer.BlockCopy(orig, 12, patched, 12 + stringLen, 8);
        Buffer.BlockCopy(
            orig,
            stringEnd + 1,
            patched,
            12 + stringLen + 8,
            orig.Length - (stringEnd + 1));

        log(Strings.T("Core.LogHeaderReordered", $"{extraA:X8}", $"{extraB:X8}"));
        return new MemoryStream(patched, writable: false);
    }

    private static MemoryStream? PatchV75(string path, Action<string> log)
    {
        byte[] orig = File.ReadAllBytes(path);
        if (orig.Length < 20)
            return null;

        uint unkA = BitConverter.ToUInt32(orig, 8);
        uint unkB = BitConverter.ToUInt32(orig, 12);
        if (unkA != 0 || unkB != 0)
        {
            log(Strings.T("Core.LogPatchSkipped", $"{unkA:X8}", $"{unkB:X8}"));
            return null;
        }

        foreach (uint targetVersion in new uint[] { 74, 73, 72, 70 })
        {
            byte[] patched = new byte[orig.Length - 8];
            Buffer.BlockCopy(orig, 0, patched, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(targetVersion), 0, patched, 4, 4);
            Buffer.BlockCopy(orig, 16, patched, 8, orig.Length - 16);

            try
            {
                using var testStream = new MemoryStream(patched, writable: false);
                var obj = _odolCtorStream.Invoke(new object[] { testStream });
                if (obj != null)
                {
                    log(Strings.T("Core.LogPatchOk", targetVersion));
                    return new MemoryStream(patched, writable: false);
                }
            }
            catch
            {
                // Try the next target version.
            }
        }

        log(Strings.T("Core.LogNoPatchWorked"));
        return null;
    }

    private static bool LooksProtectedOdol(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length < 32)
                return false;

            int sampleLength = (int)Math.Min(65552, info.Length);
            byte[] sample = new byte[sampleLength];

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int read = fs.Read(sample, 0, sample.Length);
            if (read < 32)
                return false;

            bool magic =
                sample[0] == (byte)'O' &&
                sample[1] == (byte)'D' &&
                sample[2] == (byte)'O' &&
                sample[3] == (byte)'L';
            if (!magic)
                return false;

            uint version = BitConverter.ToUInt32(sample, 4);
            uint markerA = BitConverter.ToUInt32(sample, 8);
            uint markerB = BitConverter.ToUInt32(sample, 12);
            if (version < 75 || markerA != 1 || markerB != 0)
                return false;

            int entropyCount = read - 16;
            return entropyCount > 1024 && EstimateEntropy(sample, 16, entropyCount) > 7.5;
        }
        catch
        {
            return false;
        }
    }

    private static double EstimateEntropy(byte[] bytes, int offset, int count)
    {
        int[] buckets = new int[256];
        for (int i = offset; i < offset + count; i++)
            buckets[bytes[i]]++;

        double entropy = 0;
        foreach (int bucket in buckets)
        {
            if (bucket == 0)
                continue;

            double p = (double)bucket / count;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private static uint ReadVersion(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);
        br.ReadBytes(4);
        return br.ReadUInt32();
    }

    private static string BuildOutputPath(string inputPath, string outputFolder, Action<string> log)
    {
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string safeBase = MakeObjectBuilderSafeName(baseName);

        if (safeBase != baseName)
        {
            safeBase = $"{safeBase}_{StableHash(baseName):X8}";
            log(Strings.T("Core.LogSafeOutputName", safeBase));
        }

        return Path.Combine(outputFolder, safeBase + "_debin.p3d");
    }

    private static string MakeObjectBuilderSafeName(string name)
    {
        var chars = new char[name.Length];
        int count = 0;
        bool lastWasUnderscore = false;

        foreach (char ch in name)
        {
            bool safe =
                ch is >= 'a' and <= 'z' ||
                ch is >= 'A' and <= 'Z' ||
                ch is >= '0' and <= '9' ||
                ch == '-' ||
                ch == '.';

            if (safe)
            {
                chars[count++] = ch;
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore && count > 0)
            {
                chars[count++] = '_';
                lastWasUnderscore = true;
            }
        }

        string result = new(chars, 0, count);
        result = result.Trim('.', '_');

        return result.Length >= 3 && result.Any(char.IsLetterOrDigit)
            ? result
            : "model";
    }

    private static uint StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char ch in text)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return hash;
        }
    }

    private static string Unwrap(Exception ex)
        => ex.InnerException?.Message ?? ex.Message;

    private static ConversionResult Ok(string input, string output, string message)
        => new(true, input, output, message);

    private static ConversionResult Fail(string input, string output, string message)
        => new(false, input, output, message);
}

public record ConversionResult(
    bool Success,
    string InputPath,
    string OutputPath,
    string Message);

public enum P3DFormat
{
    Unknown,
    Odol,
    Mlod
}

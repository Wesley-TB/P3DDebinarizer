using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace P3DDebin.Core;

// Lista e troca caminhos internos de arquivos .p3d (texturas, rvmats, proxies)
// inteiramente em C#, sem depender de ferramentas externas.
//
//  - MLOD: as strings sao asciiz e o formato e sequencial, entao a troca por
//    bytes e segura mesmo mudando o tamanho do caminho.
//  - ODOL: as strings tambem ficam em texto plano, mas o cabecalho guarda uma
//    tabela de offsets absolutos dos LODs. Apos a troca, essa tabela e
//    recalculada e regravada para o arquivo continuar valido.
public static class P3DRepath
{
    private static readonly string[] KnownExtensions =
        { ".paa", ".pac", ".tga", ".rvmat", ".p3d", ".bisurf", ".png", ".jpg" };

    public sealed record RepathResult(string OutputPath, int Replacements, int Sites);

    // -------------------------------------------------------------------------
    // Listagem
    // -------------------------------------------------------------------------
    public static List<string> ListPaths(string p3dPath, Action<string>? log = null)
    {
        byte[] data = File.ReadAllBytes(p3dPath);
        int scanStart = ResolveScanStart(p3dPath, data, log);
        return ScanPaths(data, scanStart, data.Length);
    }

    // -------------------------------------------------------------------------
    // Aplicacao
    // -------------------------------------------------------------------------
    public static RepathResult Apply(
        string srcPath,
        string dstPath,
        IReadOnlyList<(string From, string To)> replacements,
        Action<string>? log = null)
    {
        byte[] data = File.ReadAllBytes(srcPath);
        P3DFormat format = Converter.DetectFormat(srcPath);

        uint[] start = Array.Empty<uint>();
        uint[] end   = Array.Empty<uint>();
        int tableOffset = -1;
        int scanStart = 0;

        if (format == P3DFormat.Odol)
        {
            if (!Converter.TryReadOdolAddresses(srcPath, out start, out end))
                throw new InvalidOperationException(Strings.T("Core.ExOdolStructure"));

            scanStart   = (int)start.Where(s => s > 0).Min();
            tableOffset = FindAddressTable(data, start, end);

            if (tableOffset < 0)
                throw new InvalidOperationException(Strings.T("Core.ExOdolAddressTable"));
        }

        // Localiza todos os pontos de troca no arquivo original.
        var sites = new List<Site>();
        foreach (var (from, to) in replacements)
        {
            byte[] pat = Asciiz(from);
            byte[] rep = Asciiz(to);

            int count = 0;
            int i = scanStart;
            while ((i = IndexOf(data, pat, i, data.Length)) >= 0)
            {
                sites.Add(new Site(i, pat.Length, rep));
                i += pat.Length;
                count++;
            }

            if (count == 0)
                log?.Invoke(Strings.T("Core.LogPathNotFound", from));
            else
                log?.Invoke(Strings.T("Core.LogPathReplaced", from, to, count));
        }

        sites.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        EnsureNoOverlap(sites);

        byte[] output = Rebuild(data, sites);

        if (format == P3DFormat.Odol)
            PatchAddressTable(output, tableOffset, start, end, sites);

        File.WriteAllBytes(dstPath, output);
        return new RepathResult(dstPath, replacements.Count, sites.Count);
    }

    // -------------------------------------------------------------------------
    // Internos
    // -------------------------------------------------------------------------
    private readonly record struct Site(int Offset, int OldLength, byte[] Replacement)
    {
        public int Delta => Replacement.Length - OldLength;
        public int End   => Offset + OldLength;
    }

    private static int ResolveScanStart(string p3dPath, byte[] data, Action<string>? log)
    {
        if (Converter.DetectFormat(p3dPath) != P3DFormat.Odol)
            return 0;

        // No ODOL, ignora o prefixo do cabecalho (o proprio nome do modelo) e
        // varre apenas a regiao dos LODs, onde ficam texturas/rvmats/proxies.
        if (Converter.TryReadOdolAddresses(p3dPath, out uint[] start, out _))
        {
            uint[] real = start.Where(s => s > 0).ToArray();
            if (real.Length > 0)
                return (int)real.Min();
        }

        log?.Invoke(Strings.T("Core.LogOdolStructNotRead"));
        return 0;
    }

    private static List<string> ScanPaths(byte[] data, int start, int end)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        int i = Math.Max(0, start);
        while (i < end)
        {
            if (!IsPrintable(data[i]))
            {
                i++;
                continue;
            }

            int j = i;
            while (j < end && IsPrintable(data[j]))
                j++;

            if (j < end && data[j] == 0x00)
            {
                string s = Encoding.ASCII.GetString(data, i, j - i);
                if (LooksLikePath(s) && seen.Add(s))
                    result.Add(s);
            }

            i = j + 1;
        }

        return result;
    }

    private static bool IsPrintable(byte b) => b is >= 0x20 and <= 0x7E;

    private static bool LooksLikePath(string s)
    {
        if (s.Length < 3)
            return false;

        if (s.StartsWith("proxy:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!s.Contains('\\'))
            return false;

        return KnownExtensions.Any(ext => s.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] Asciiz(string text)
    {
        byte[] body = Encoding.ASCII.GetBytes(text);
        byte[] result = new byte[body.Length + 1];
        Buffer.BlockCopy(body, 0, result, 0, body.Length);
        return result; // ultimo byte ja e 0x00
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from, int to)
    {
        int limit = to - needle.Length;
        for (int i = from; i <= limit; i++)
        {
            int k = 0;
            while (k < needle.Length && haystack[i + k] == needle[k])
                k++;

            if (k == needle.Length)
                return i;
        }
        return -1;
    }

    private static void EnsureNoOverlap(List<Site> sites)
    {
        for (int i = 1; i < sites.Count; i++)
        {
            if (sites[i].Offset < sites[i - 1].End)
                throw new InvalidOperationException(Strings.T("Core.ExOverlappingChanges"));
        }
    }

    private static byte[] Rebuild(byte[] data, List<Site> sites)
    {
        if (sites.Count == 0)
            return (byte[])data.Clone();

        long newLength = data.Length + sites.Sum(s => (long)s.Delta);
        using var ms = new MemoryStream((int)newLength);

        int cursor = 0;
        foreach (Site site in sites)
        {
            ms.Write(data, cursor, site.Offset - cursor);
            ms.Write(site.Replacement, 0, site.Replacement.Length);
            cursor = site.End;
        }
        ms.Write(data, cursor, data.Length - cursor);

        return ms.ToArray();
    }

    // Procura a tabela = startAdresses[] seguida de endAdresses[] (uint32 LE).
    private static int FindAddressTable(byte[] data, uint[] start, uint[] end)
    {
        byte[] pattern = new byte[(start.Length + end.Length) * 4];
        int p = 0;
        foreach (uint v in start) { WriteUInt32(pattern, p, v); p += 4; }
        foreach (uint v in end)   { WriteUInt32(pattern, p, v); p += 4; }

        return IndexOf(data, pattern, 0, data.Length);
    }

    private static void PatchAddressTable(
        byte[] output, int tableOffset, uint[] start, uint[] end, List<Site> sites)
    {
        int p = tableOffset;
        foreach (uint addr in start)
        {
            WriteUInt32(output, p, Shift(addr, sites));
            p += 4;
        }
        foreach (uint addr in end)
        {
            WriteUInt32(output, p, Shift(addr, sites));
            p += 4;
        }
    }

    // Desloca um offset absoluto pela soma dos deltas das trocas antes dele.
    private static uint Shift(uint address, List<Site> sites)
    {
        if (address == 0)
            return 0;

        int delta = 0;
        foreach (Site site in sites)
        {
            if (site.Offset < address)
                delta += site.Delta;
        }
        return (uint)(address + delta);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset]     = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }
}

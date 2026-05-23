using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using P3DDebin.Core;

namespace P3DDebin;

// CLI mode for headless / pipeline usage.
//
// Activated when Program.Main receives any arguments.
// Since the app is a WinExe (no console attached), we attach to the parent
// console so stdout/stderr show up where the user invoked us.
//
// Examples:
//   P3DDebin.exe debin "model.p3d"
//   P3DDebin.exe debin "Addons" -o ".\out"
//   P3DDebin.exe rvmat "model.p3d"
//   P3DDebin.exe cfg "Addons" -o ".\cfgs"
//   P3DDebin.exe repath "model.p3d" --rule "old\=new\" --rule "foo.paa=bar.paa"
//   P3DDebin.exe list-paths "model.p3d"
internal static class Cli
{
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int dwProcessId);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    [DllImport("kernel32.dll")] private static extern bool FreeConsole();
    private const int ATTACH_PARENT_PROCESS = -1;

    public static int Run(string[] args)
    {
        // Try to attach to the parent console; if there isn't one (e.g. launched
        // by a GUI shell), allocate our own so output is at least visible.
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
            AllocConsole();

        try
        {
            return Dispatch(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        finally
        {
            Console.Out.Flush();
            FreeConsole();
        }
    }

    private static int Dispatch(string[] args)
    {
        string cmd = args[0].ToLowerInvariant();

        if (cmd is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (cmd is "-v" or "--version" or "version")
        {
            var v = typeof(Cli).Assembly.GetName().Version;
            Console.WriteLine($"P3DDebin {v}");
            return 0;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine($"error: command '{cmd}' requires an input path. Run --help.");
            return 2;
        }

        string input = args[1];
        string? outDir = null;
        var rules = new List<(string From, string To)>();

        for (int i = 2; i < args.Length; i++)
        {
            string a = args[i];
            if ((a == "-o" || a == "--out") && i + 1 < args.Length)
            {
                outDir = args[++i];
            }
            else if ((a == "-r" || a == "--rule") && i + 1 < args.Length)
            {
                string r = args[++i];
                int eq = r.IndexOf('=');
                if (eq <= 0)
                {
                    Console.Error.WriteLine($"error: --rule must be 'from=to', got '{r}'");
                    return 2;
                }
                rules.Add((r[..eq], r[(eq + 1)..]));
            }
            else
            {
                Console.Error.WriteLine($"warning: unknown argument '{a}' ignored.");
            }
        }

        if (!File.Exists(input) && !Directory.Exists(input))
        {
            Console.Error.WriteLine($"error: input not found: {input}");
            return 2;
        }

        string[] files = ResolveInputFiles(input);
        if (files.Length == 0)
        {
            Console.Error.WriteLine("error: no .p3d files matched the input.");
            return 2;
        }

        return cmd switch
        {
            "debin"      => RunDebin(files, outDir),
            "rvmat"      => RunRvmat(files, outDir),
            "cfg"        => RunCfg(files, outDir),
            "repath"     => RunRepath(files, rules, outDir),
            "list-paths" => RunListPaths(files),
            _ => Unknown(cmd)
        };
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"error: unknown command '{cmd}'. Run --help.");
        return 2;
    }

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    private static int RunDebin(string[] files, string? outDir)
    {
        int ok = 0, fail = 0, skip = 0;
        foreach (string f in files)
        {
            if (Converter.DetectFormat(f) != P3DFormat.Odol) { Skip(f, "not ODOL"); skip++; continue; }
            string dst = outDir ?? Path.GetDirectoryName(f)!;
            Directory.CreateDirectory(dst);
            Console.WriteLine($"[debin] {f}");
            var r = Converter.Convert(f, dst, s => Console.WriteLine("  " + s));
            if (r.Success) { Console.WriteLine($"  -> {r.OutputPath}"); ok++; }
            else { Console.Error.WriteLine($"  FAIL: {r.Message}"); fail++; }
        }
        return Summarize(ok, fail, skip);
    }

    private static int RunRvmat(string[] files, string? outDir)
    {
        int ok = 0, fail = 0, skip = 0;
        foreach (string f in files)
        {
            if (Converter.DetectFormat(f) != P3DFormat.Odol) { Skip(f, "not ODOL"); skip++; continue; }
            string dst = outDir ?? Path.GetDirectoryName(f)!;
            Directory.CreateDirectory(dst);
            Console.WriteLine($"[rvmat] {f}");
            try
            {
                int n = OdolExtract.ExtractRvmats(f, dst, s => Console.WriteLine("  " + s));
                Console.WriteLine($"  -> {n} rvmat(s) under {dst}");
                ok++;
            }
            catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }
        }
        return Summarize(ok, fail, skip);
    }

    private static int RunCfg(string[] files, string? outDir)
    {
        int ok = 0, fail = 0, skip = 0;
        foreach (string f in files)
        {
            if (Converter.DetectFormat(f) != P3DFormat.Odol) { Skip(f, "not ODOL"); skip++; continue; }
            string dst = outDir ?? Path.GetDirectoryName(f)!;
            Directory.CreateDirectory(dst);
            Console.WriteLine($"[cfg] {f}");
            try
            {
                string target = OdolExtract.ExtractModelCfg(f, dst, s => Console.WriteLine("  " + s));
                Console.WriteLine($"  -> {target}");
                ok++;
            }
            catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }
        }
        return Summarize(ok, fail, skip);
    }

    private static int RunRepath(string[] files, List<(string From, string To)> rules, string? outDir)
    {
        if (rules.Count == 0)
        {
            Console.Error.WriteLine("error: repath needs at least one --rule \"from=to\".");
            return 2;
        }

        int ok = 0, fail = 0;
        foreach (string f in files)
        {
            string dir = outDir ?? Path.GetDirectoryName(f)!;
            Directory.CreateDirectory(dir);
            string dst = Path.Combine(dir, Path.GetFileNameWithoutExtension(f) + "_repath.p3d");
            Console.WriteLine($"[repath] {f}");
            try
            {
                var r = P3DRepath.Apply(f, dst, rules, s => Console.WriteLine("  " + s));
                Console.WriteLine($"  -> {r.OutputPath}  ({r.Replacements} replacement(s), {r.Sites} site(s))");
                ok++;
            }
            catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); fail++; }
        }
        return Summarize(ok, fail, 0);
    }

    private static int RunListPaths(string[] files)
    {
        foreach (string f in files)
        {
            Console.WriteLine($"[paths] {f}");
            try
            {
                var list = P3DRepath.ListPaths(f);
                foreach (string p in list)
                    Console.WriteLine($"  {p}");
                Console.WriteLine($"  ({list.Count} path(s))");
            }
            catch (Exception ex) { Console.Error.WriteLine($"  FAIL: {ex.Message}"); }
        }
        return 0;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string[] ResolveInputFiles(string input)
    {
        if (Directory.Exists(input))
            return Directory.GetFiles(input, "*.p3d", SearchOption.AllDirectories);

        if (File.Exists(input))
            return new[] { input };

        return Array.Empty<string>();
    }

    private static void Skip(string f, string why) => Console.WriteLine($"[skip] {f}  ({why})");

    private static int Summarize(int ok, int fail, int skip)
    {
        Console.WriteLine();
        Console.WriteLine($"done: {ok} ok, {fail} failed, {skip} skipped");
        return fail == 0 ? 0 : 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"P3DDebin — Arma 3 .p3d processor

Usage:
  P3DDebin.exe                                  Launch the GUI.
  P3DDebin.exe <command> <input> [options]      Headless mode.

Commands:
  debin       <input> [-o <dir>]                Debinarize ODOL -> MLOD (_debin.p3d).
  rvmat       <input> [-o <dir>]                Extract embedded RVMATs.
  cfg         <input> [-o <dir>]                Reconstruct model.cfg (with Animations).
  repath      <input> --rule from=to [...] [-o <dir>]
                                                Replace internal paths (writes <name>_repath.p3d).
  list-paths  <input>                           Print every internal path referenced.

  --help, -h                                    This message.
  --version, -v                                 Version info.

<input> may be a single .p3d file or a folder (recurses into subfolders).
-o / --out  defaults to the input file's own folder.
-r / --rule may be passed multiple times for repath.

Examples:
  P3DDebin.exe debin ""C:\mod\Addons""
  P3DDebin.exe cfg ""C:\mod\hilux.p3d"" -o ""C:\mod\cfg""
  P3DDebin.exe repath ""hilux.p3d"" --rule ""oldmod\=newmod\"" --rule ""body.paa=bodyV2.paa""
  P3DDebin.exe list-paths ""hilux.p3d""

Exit codes: 0 = ok, 1 = some file(s) failed, 2 = usage error.");
    }
}

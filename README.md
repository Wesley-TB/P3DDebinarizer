# P3D Debinarizer

A Windows desktop utility for inspecting and processing Arma 3 `.p3d` model files (ODOL / MLOD).
Built on .NET 8 + WinForms, distributed as a single self-contained `.exe`.

![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)
![Framework](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Table of contents

- [Download & install](#download--install)
- [Quick start](#quick-start)
- [Interface overview](#interface-overview)
- [Features in detail](#features-in-detail)
  - [Debinarize (ODOL → MLOD)](#1-debinarize-odol--mlod)
  - [Extract RVMATs](#2-extract-rvmats)
  - [Extract model.cfg](#3-extract-modelcfg)
  - [Change internal paths (repath)](#4-change-internal-paths-repath)
- [Batch mode](#batch-mode)
- [Drag-and-drop](#drag-and-drop)
- [Language](#language)
- [Command-line mode (headless / CI)](#command-line-mode-headless--ci)
- [Build from source](#build-from-source)
- [Troubleshooting](#troubleshooting)
- [Limitations](#limitations)
- [License & credits](#license--credits)

---

## Download & install

1. Grab the latest **`P3DDebin.exe`** (~420 KB) from the [Releases](https://github.com/Wesley-TB/P3DDebinarizer/releases) page.
2. Put it anywhere — Desktop, a tools folder, wherever.
3. Double-click.

**Requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).** If it's not installed yet, Windows will prompt you to install it on first launch — one click, ~30s, never again.

If Windows SmartScreen warns about an unknown publisher, click **More info → Run anyway** (the build is not code-signed).

---

## Quick start

### Open a single file
1. Click **File…** and pick a `.p3d`, *or* drag-and-drop a `.p3d` onto the window.
2. The output folder defaults to the file's own folder — change it with **Select…** if you want.
3. Click the action you want:
   - **Debinarize** to get an editable MLOD.
   - **Extract RVMATs** to dump every embedded material.
   - **Extract model.cfg** to rebuild the config.
   - **Change paths** to bulk-rewrite internal paths.

### Open a whole folder
1. Click **Folder…** (or drop a folder onto the window).
2. The app recursively finds every `*.p3d` under it.
3. Same buttons — they now run on every file in the folder.

That's it. Watch the log panel for per-file progress.

---

## Interface overview

```
┌─────────────────────────────────────────────────────────────────┐
│ P3D Debinarizer                                  Language: [ ▼ ]│
│ Subtitle line                                                   │
├─────────────────────────────────────────────────────────────────┤
│ Input:  [ path / drop here              ] [ File… ] [ Folder… ]│
│ Output: [ path                          ] [ Select… ]          │
├─────────────────────────────────────────────────────────────────┤
│ [ Change paths ] [ Debinarize ] [ Extract RVMATs ] [ model.cfg ]│
├─────────────────────────────────────────────────────────────────┤
│ Log                                                             │
│   timestamped messages…                                         │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│ Status: ready / busy / done                                     │
└─────────────────────────────────────────────────────────────────┘
```

**Buttons auto-enable** based on what you loaded:
- `Change paths` requires any `.p3d` (ODOL or MLOD).
- `Debinarize`, `Extract RVMATs`, `Extract model.cfg` require ODOL (binarized) files.
- If you load a folder, all four become available and operate in batch.

---

## Features in detail

### 1. Debinarize (ODOL → MLOD)
Converts a binarized `.p3d` back to MLOD form so you can open it in Object Builder.

- **Output**: written to the chosen output folder with the suffix `_debin.p3d` (e.g. `hilux_20.p3d` → `hilux_20_debin.p3d`).
- **Safe naming**: invalid characters in the source name are normalised so Object Builder can open the file.
- **Batch**: every `.p3d` in the folder is processed; non-ODOL files are skipped with a log entry.

### 2. Extract RVMATs
Rebuilds every `EmbeddedMaterial` carried inside the ODOL as a standalone `.rvmat` text file.

- **Folder layout preserved**: the original material path is recreated under the output folder (e.g. `tacdev\hilux\data\body.rvmat`).
- **pboProject `-O` obfuscation handled**: when the material name is mangled with non-printable bytes, the file is renamed to `obfuscated_material_NNN.rvmat`. The *content* of the rvmat is untouched.
- **Deduplicated**: identical material names across LODs are written once.

### 3. Extract model.cfg
Rebuilds a usable `model.cfg` from the ODOL data — not a stub, the full thing.

What you get:

- **`class CfgSkeletons`** — skeleton name, `isDiscrete`, full bone/parent hierarchy, `pivotsModel` when present.
- **`class CfgModels`** — `skeletonName`, `sectionsInherit`, `sections[]` collected from sectional named selections across all LODs.
- **`class Animations`** — every animation reconstructed:
  - `type`, `source`, `sourceAddress`
  - `selection` resolved through the bone-to-anim mapping
  - `axis` resolved by matching `axisPos` against named selections in the Memory LOD (falls back to `<selection>_axis` if no match)
  - `minValue`, `maxValue`, `animPeriod`, `initPhase`
  - Type-specific: `angle0`/`angle1` (rotation), `offset0`/`offset1` (translation), `hideValue` (hide), `angle`/`axisOffset` (direct)

> Output is written as `<original>.cfg` in the chosen output folder.

### 4. Change internal paths (repath)
Bulk-rewrite the internal asset references (textures, RVMATs, proxies, `.bisurf`, `.p3d`, `.png`, `.jpg`) inside `.p3d` files **without** debinarizing them first.

How it works:

1. Click **Change paths**. The app scans the loaded `.p3d`(s) and opens a dialog.
2. **File list (left)** — every loaded model with a checkbox so you can include/exclude individuals.
3. **Rules list (right)** — add `find → replace` pairs. Rules apply to every path on every checked file.
4. Confirm to apply.

Output naming:
- **Single mode**: writes `<original>_repath.p3d` in the output folder. The source is never touched.
- **Batch mode**: writes `<original>_repath.p3d` next to each input file. The source is never touched.

Use cases:
- Rebranding a model pack to a new mod prefix (`oldmod\…` → `newmod\…`).
- Fixing case-mismatched paths after moving files.
- Pointing proxies at a different model.

---

## Batch mode

Drop a folder (or pick one with **Folder…**) and *any* of the four actions will run across every `.p3d` it finds — recursively, including subfolders.

- ODOL-only actions skip MLOD/non-ODOL files with a log line, then keep going.
- A summary line at the end shows ok / skipped / failed counts.
- The UI is locked while a batch is running so you can't accidentally start a second one.

---

## Drag-and-drop

You can drop onto the window:
- A single `.p3d` file → switches to single-file mode.
- A folder → switches to batch mode.
- Multiple files / mixed → the first valid target wins.

---

## Language

Switch the UI language at runtime using the combo in the header. Supported:
- 🇺🇸 English
- 🇧🇷 Português (Brasil)
- 🇩🇪 Deutsch
- 🇪🇸 Español

All labels, dialogs and log messages are translated.

---

## Command-line mode (headless / CI)

Pass any argument and the same exe runs as a CLI instead of opening the GUI. Output goes to the parent console (cmd / PowerShell).

```
P3DDebin.exe <command> <input> [options]

Commands:
  debin       <input> [-o <dir>]              Debinarize ODOL -> MLOD (_debin.p3d)
  rvmat       <input> [-o <dir>]              Extract embedded RVMATs
  cfg         <input> [-o <dir>]              Reconstruct model.cfg (with Animations)
  repath      <input> --rule from=to [...]    Replace internal paths (_repath.p3d)
              [-o <dir>]
  list-paths  <input>                         Print every internal path referenced

  --help / -h        Help
  --version / -v     Version
```

`<input>` is a file or folder (folders are recursed). `-o` defaults to the input's own folder.
Exit codes: `0` ok, `1` some file(s) failed, `2` usage error.

### Running the exe from a shell

**CMD** runs it directly:
```cmd
P3DDebin.exe --help
```

**PowerShell** refuses to run executables from the current directory unless you prefix `.\`:
```powershell
.\P3DDebin.exe --help
```

Or pass the full path from anywhere:
```powershell
C:\path\to\P3DDebin.exe --help
```

To make `P3DDebin.exe` work as a global command in any shell, add its folder to your `PATH`:

1. `Win+R` → `sysdm.cpl` → **Advanced** tab → **Environment Variables**
2. Edit `Path` (User or System) → **New** → paste the folder containing `P3DDebin.exe`
3. **Open a new shell** (existing windows keep the old PATH)

To reload PATH in the current PowerShell session without reopening:
```powershell
$env:Path = [Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [Environment]::GetEnvironmentVariable("Path","User")
```

### Examples

Batch debinarize an entire `Addons` folder:
```
P3DDebin.exe debin "C:\mod\Addons"
```

Extract model.cfg from one model into a specific folder:
```
P3DDebin.exe cfg "C:\mod\hilux.p3d" -o "C:\mod\cfgs"
```

Rebrand a mod by repathing every model:
```
P3DDebin.exe repath "C:\mod\Addons" --rule "oldmod\=newmod\" -o "C:\out"
```

Inspect what paths a model references (no changes written):
```
P3DDebin.exe list-paths "C:\mod\hilux.p3d"
```

Chain multiple repath rules:
```
P3DDebin.exe repath "model.p3d" --rule "body.paa=bodyV2.paa" --rule "z\old=z\new"
```

---

## Build from source

You need:
- **.NET 8 SDK** (Windows)
- A copy of **`BisDll.dll`** placed at `P3DDebin\BisDll.dll` (proprietary Bohemia component, not redistributed in this repo — obtain it from your own Arma tools setup)

Then:

```bat
build.bat
```

This calls `dotnet publish` with `-r win-x64 --self-contained true -p:PublishSingleFile=true` and produces `release\P3DDebin.exe`.

For a quick debug build only:

```bat
cd P3DDebin
dotnet build -c Release
```

---

## Troubleshooting

**"Could not load the ODOL (protected file or unsupported version)."**
The file is either obfuscated by `pboProject` with options the BisDll loader cannot handle, or it's a newer ODOL revision than supported. Try the source MLOD if you have it.

**RVMATs come out named `obfuscated_material_NNN.rvmat`.**
That's expected for `pboProject -O` content: the material *name* was scrambled but the *content* is intact. Rename them yourself if you need the originals.

**Animations look incomplete compared to the source `model.cfg`.**
Binarization drops animation classes whose bones aren't actually referenced. The extractor only reconstructs what survived in the ODOL. Wheels 3 and 4 on a model where only wheels 1 and 2 are bound is a typical case.

**SmartScreen blocks the exe.**
Click *More info → Run anyway*. The build is not code-signed.

**Slow first launch.**
Single-file executables self-extract once into `%TEMP%\.net\P3DDebin\…`. Subsequent launches are fast.

---

## Limitations

- Animations can only be reconstructed for what survives ODOL binarization.
- Some heavily obfuscated `pboProject` output may not load at all.
- Windows x64 only; no Linux / macOS / ARM builds.

---

## License & credits

- App code: MIT (see source).
- `BisDll.dll`: proprietary, © Bohemia Interactive — **not** included in this repo.
- Inspired by Eliteness for the model.cfg / skeleton reconstruction approach.

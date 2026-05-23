# P3D Debinarizer — Features

A Windows desktop utility for inspecting and processing Arma 3 `.p3d` model files (ODOL / MLOD). Built on .NET 8 + WinForms, distributed as a single self-contained `.exe`.

## Input modes

- **Single file**: pick one `.p3d` file via dialog or drag-and-drop.
- **Batch (folder)**: pick a folder; the app recursively scans for all `*.p3d` files in subfolders.
- **Format detection**: each file is probed and tagged as ODOL (binarized) or MLOD (editable) — actions that only apply to one format are auto-disabled.

## Core actions

### 1. Debinarize (ODOL → MLOD)
Converts a binarized `.p3d` back into MLOD form, openable in Object Builder. Output is written next to the original with `_debin.p3d` suffix.

### 2. Extract RVMATs
Reconstructs every `EmbeddedMaterial` carried inside the ODOL as a standalone `.rvmat` text file, preserving the original folder layout from the material path. Handles `pboProject -O` obfuscated material names by renaming them to `obfuscated_material_NNN.rvmat` (content kept intact).

### 3. Extract model.cfg
Rebuilds a complete `model.cfg` from the ODOL data, including:
- `CfgSkeletons` — skeleton name, `isDiscrete`, full bone/parent hierarchy, `pivotsModel` when present.
- `CfgModels` — `skeletonName`, `sectionsInherit`, and the `sections[]` list collected from sectional named selections across all LODs.
- `class Animations` — every animation class with `type`, `source`, `selection`, `axis`, `sourceAddress`, `minValue`/`maxValue`, `angle0/1` or `offset0/1` or `hideValue`, `animPeriod`, `initPhase`. The `selection` is resolved via the bone-to-anim map, and the `axis` is resolved by matching `axisPos` against named selections in the Memory LOD (falling back to `<selection>_axis`).

### 4. Change internal paths (repath)
Lists every internal asset reference (textures, RVMATs, proxies, .bisurf, .p3d, .png, .jpg) inside the selected `.p3d` files and lets you bulk-rewrite them through a rules dialog:
- Per-file checkbox so you can include / exclude individual models.
- Rule list with find / replace pairs applied to every matching path.
- **Single mode**: writes a new file named `<original>_repath.p3d` in the output folder, leaving the source untouched.
- **Batch mode**: writes a new `<original>_repath.p3d` next to each input file.

## Batch operations
All three ODOL-only actions (Debinarize, Extract RVMATs, Extract model.cfg) work on a whole folder. Non-ODOL files are skipped with a log entry; the rest run in sequence with per-file success / failure reporting.

## UI conveniences
- **Drag-and-drop**: drop a file or folder anywhere on the window to set the input.
- **Output folder**: defaults to the input's directory; can be overridden.
- **Live log panel**: timestamped progress, error and success lines.
- **Status bar**: current activity and "busy" lock that disables actions during processing.
- **Multi-language UI**: English, Portuguese (BR), German, Spanish — switchable from the language combo in the header at runtime; all labels, dialogs and log strings are localized.

## Packaging
- Self-contained Windows x64 single-file `.exe` (`release\P3DDebin.exe`).
- .NET 8 runtime, WinForms and the embedded `BisDll` are bundled — no separate install required on the target machine.
- `build.bat` rebuilds the release into the `release` folder using `dotnet publish` with single-file + compression.

## Out of scope / known limitations
- Animation reconstruction reflects only what survives binarization. Animation classes the binarizer dropped (because the bone isn't referenced) will not reappear.
- Some `pboProject`-protected models can refuse to load; the app surfaces the underlying error rather than silently failing.

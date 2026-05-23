# P3D Debinarizer

Windows desktop utility for inspecting and processing Arma 3 `.p3d` model files (ODOL / MLOD).

See [FEATURES.md](FEATURES.md) for the full feature list.

## Build

Requires the .NET 8 SDK and a copy of `BisDll.dll` placed at `P3DDebin/BisDll.dll` (not distributed in this repository — obtain it from your own Arma tools setup).

```
build.bat
```

The single-file self-contained `.exe` is produced under `release/`.

## Note

`BisDll.dll` is a proprietary Bohemia Interactive component and is intentionally excluded from this repository. You must supply your own copy to compile.

# Third-party notices

## xjb — fast float to string algorithm

LuminPack's JSON float/double formatting is a C# port of the xjb algorithm.

| | |
|---|---|
| Project | https://github.com/xjb714/xjb |
| Locked commit | `80cc89574a8f8457ffbf951afa2fd27c2459bd4a` (2026-08-10) |
| Paper | Xiang / Wang, *xjb: Fast Float to String Algorithm*, preprint 202511.1698 |
| License | Apache License 2.0 (see `xjb-LICENSE.txt` in this directory) |

Ported material and its C# destination:

| Upstream file | What was ported | Destination |
|---|---|---|
| `bench/xjb/float_to_decimal/xjb32_i.cpp` (`xjb_v2_f32_to_dec`) | binary32 shortest-decimal core, `pow10_table_reverse`, `h37` | `src/LuminPack.Core/Core/Json/Xjb/XjbFloat32.cs`, `XjbTables.g.cs` |
| `bench/xjb/float_to_decimal/xjb64_i.cpp` (`xjb64_v2_f64_to_dec`) | binary64 shortest-decimal core (x64 path), `g[1234]` | `src/LuminPack.Core/Core/Json/Xjb/XjbFloat64.cs`, `XjbTables.g.cs` |
| `bench/xjb/float_to_decimal/xjb64_i.cpp` | `h7[2048]` used as a cross-check for the portable `h` formula | verified in `tests/LuminPack.Tests/JsonXjbFormatterTest.cs` |

Notes:

- The upstream README states "MIT License", but the repository's `LICENSE` file is the Apache
  License 2.0. The license file governs and its full text is included as `xjb-LICENSE.txt`.
- The lookup tables are regenerated from the upstream sources by
  `tools/xjb-tables/extract_tables.py`, which also re-derives the `h` shifts and prints FNV-1a
  hashes that the unit tests assert against.
- The JSON layout policy and the emitter in
  `src/LuminPack.Core/Core/Json/Xjb/LuminPackJsonFloatFormatter.cs` are LuminPack's own; they are not
  copied from `src/ftoa_json.cpp`. Upstream `nan`/`inf` text is deliberately not used because JSON
  only allows finite numbers.
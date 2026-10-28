# String transcoder comparison

This probe measures only preallocated UTF-8 encoding/decoding and UTF-16 encoding. It does not run the serializer benchmark suite. Keep all retained assemblies, native listings and reports under an ignored directory such as `.tmp`. Timing runs are opt-in and can compete with other applications.

Build the Release runtime first. Build `LuminPack.StringPerformanceProbe.Fixture/LuminPack.StringPerformanceProbe.Fixture.csproj` three times with identical source and compiler settings, retaining each output directory with `-o`:

- Original runtime: `-p:LuminPackPath=<absolute-original-LuminPack.Core.dll> -p:ProfileApi=false`.
- Runtime immediately before adding the profile API: `-p:LuminPackPath=<absolute-pre-profile-LuminPack.Core.dll> -p:ProfileApi=false`.
- Candidate runtime: `-p:LuminPackPath=<absolute-candidate-LuminPack.Core.dll>`.

Use `-t:Rebuild` for each fixture image so an incremental build cannot retain an earlier reference. The fixture targets .NET 11; use matching runtime images and run all variants under the same installed runtime. Then build this probe and run its executable with:

```text
<original-image-directory> <pre-profile-image-directory> <candidate-image-directory> <ignored-results.json>
```

The probe validates outputs before timing, shares the exact same strings and buffers across images to control data alignment, calibrates each case, and rotates measurement order over nine samples. Results include paired reference ratios, raw samples, interquartile dispersion and per-operation managed allocations. Repeat complete processes and compare the same shape and length; a single absolute nanosecond result is insufficient evidence. UTF-8 uses the platform transcoder as its common reference. UTF-16 uses the unchanged original API. Setup, reflection and output formatting are excluded from intervals.

Set `DOTNET_TieredCompilation=0` for repeatable fully optimized code without tier transitions. Separately test the default tiering policy when it matters to a consuming application. Set `LUMINPACK_STRING_CODEGEN=1` with `DOTNET_JitDisasm` and `DOTNET_JitStdOutFile` to exercise the closed wrappers without measuring time. A profile removes runtime encoding selection; a constant mode argument can already produce identical machine code.

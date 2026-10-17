# Repository constraints

Read [the mechanism contract](docs/serialization-mechanism-contract.md) and [CONTRIBUTING](CONTRIBUTING.md) before changing production or generator code. They apply to the entire repository, including generated code, runtime helpers, mapping, and conditional targets.

Preserve every established mechanism, not only wire compatibility: Local + As storage views, virtual Union dispatch, generic Union virtual plus cached-slot dispatch, version-tolerant length tables, reference tracking, value/unmanaged paths, constructor/pool/reuse behavior, callbacks, specialized formatters, state ownership, and all remaining mechanisms in the contract. Do not substitute another design without explicit user authorization. Record approved differences and distinguish correctness fixes from compatibility refactors.

Faster alternative mechanisms may be proposed. First prepare an isolated, reviewable experiment with before/after reference ratios, allocations, generated IL/native evidence, and its semantic/API/compatibility tradeoffs. A mechanism may be replaced only after the user explicitly approves that concrete proposal. Preserve the existing production mechanism while evaluating an unapproved alternative; a performance target alone is not approval to change it.

Preserve the fixed-length bounds-check policy: primitives, unmanaged values, structs, and fixed field batches have no per-value bounds checks in Release. Keep dynamic capacity/input checks at arrays, collections, and string boundaries, including their bulk payloads. Existing DEBUG assertions remain permitted. Do not add blanket checks as part of refactoring or performance work.

Preserve architecture-specific fixed unmanaged access: 32-bit paths must use `Unsafe.CopyBlockUnaligned` and the existing packed storage views; typed `Unsafe.WriteUnaligned` / `Unsafe.ReadUnaligned` must not execute there because they can crash IL2CPP. On 64-bit, both typed unsafe access and block copies are allowed. Retain the adjacent-field bulk-copy optimization with actual field-address proofs, volatile exclusion, and a field-wise fallback. Variable-length collection bulk copies retain their separate established paths.

Performance decisions must account for local machine variability. Prioritize LuminPack's elapsed-time ratios against other libraries measured on the same machine, fixture, runtime, options, and run; retain absolute times as supporting evidence. Report before/after ratios against the same references, repeated samples and dispersion, and allocations. Do not infer a win from an isolated nanosecond result or assume all workloads fluctuate identically.

Use English for code, comments, XML, diagnostics, tests, and maintenance documents; README is the exception. Do not remove storage placeholders or externally usable/generated members based only on ordinary call-site searches. Run the relevant structural, historical, runtime, and incremental checks specified by the contract.

README is project introduction, installation and usage documentation. Do not put release notes, version-change summaries, upgrade/migration notices or development/performance experiment reports in README. Keep those records under docs. Installation examples may reference the current package version without describing version changes.

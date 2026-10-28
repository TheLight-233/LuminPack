# Agent instructions

[中文](AGENTS.cn.md)

LuminPack is a source-generated binary and JSON serializer for .NET and Unity. Read these before changing code:

- [Mechanism contract](docs/mechanism-contract.md): the hard constraints. It is binding for runtime code, the generator, generated code, optional modules and conditional targets.
- [Architecture](docs/architecture.md): where things live and how they fit together.
- [Contributing](CONTRIBUTING.md): build, checks and conventions.

## Hard rules

1. **Keep every established mechanism.** Local + As storage views, virtual Union dispatch and its cached generic slots, version-tolerant name tables, reference tracking, raw unmanaged paths and `StructLayout`, constructor/pool/reuse behavior, callbacks, specialized formatters, registration through method pointers, compiled options and state ownership. A faster design needs an isolated experiment in `experiments/` with same-run reference ratios, allocations and IL/native evidence, and the owner's explicit approval of that concrete proposal. A performance goal is not approval.
2. **Reader checks stay on in Release.** `EnsureReadable` and its callers (`Advance`, fixed storage batches, Union framing) run in Debug and Release. Generated cursor arithmetic keeps its check before moving the cursor. Collection, string and bulk-payload checks stay. Fixed Writer stores and address-only raw kernels keep their separate unchecked contracts.
3. **32-bit fixed access.** Only individual unmanaged values of at most four bytes may use typed unaligned access on 32-bit; larger values and field batches use `LuminPackMarshal.CopyBlockUnaligned` and packed views (IL2CPP). Adjacent-field bulk copies need real field-address proofs, exclude volatile fields and keep a field-wise fallback. Only `LuminPackMarshal.CopyBlockUnaligned` calls `Unsafe.CopyBlockUnaligned`.
4. **SIMD.** Review scalar, 128/256/512-bit and ARM64 paths; guard by exact ISA features; keep bounded tails and a portable fallback; never touch memory outside a validated range. Unavailable hardware needs CI execution.
5. **Performance evidence** is LuminPack's time divided by the reference library's, same machine, fixture, runtime, options and run, with repeated samples, dispersion and allocations. One isolated figure proves nothing; the machine is noisy.
6. **Language.** English for code, comments, XML, diagnostics, tests and maintenance documents. README files and `*.cn.md` editions are the exception.
7. **Documents.** README is the cover page; feature documentation lives in `docs`. Each document has an English `name.md` and a Chinese `name.cn.md`; update both. No release notes, version histories or experiment reports in README or `docs`.
8. **Don't remove** storage placeholders or externally usable/generated members based only on call-site searches.
9. **Don't commit** unless asked.

## Working practice

- Prefer focused tests (one framework, a `--filter`, a test area or one `--verify-<area>` selector) over the full suites, which are slow.
- Report results honestly: what ran, what failed, what was not verified.
- When public usage changes, update the docs (both editions) and `skills/luminpack`.

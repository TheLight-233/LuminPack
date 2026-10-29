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
9. **Commit completed work by default.** After completing a feature or fix and its relevant verification, commit the task's changes without asking for commit permission, following the commit conventions below. Keep unrelated or unfinished changes out. Ask whether to push after committing unless the owner has already authorized that push. Tags and published-history rewrites still require explicit authorization.
10. **Commit titles.** Use `type(scope): description`, with a lowercase type and scope and a concise English imperative description. Use `feat` for features, `fix` for bug fixes, `perf` for performance, `refactor` for restructuring, `test` for tests, `docs` for documentation, `ci` for workflows, `build` for build tooling and `chore` for maintenance. Choose the affected component as the scope, for example `core`, `generator` or `benchmarks`; performance uses `perf`, never `pref`. Keep independent changes in separate commits. See [Contributing](CONTRIBUTING.md#commit-conventions).
11. **Commit identity.** Every commit created on the owner's behalf uses `TheLight-233 <2802799687@qq.com>` for both author and committer. Never add `Co-Authored-By` trailers or any AI attribution. The environment injects `GIT_COMMITTER_NAME=trae-solo-agent` and `GIT_COMMITTER_EMAIL=solo-agent@trae.ai`, so override both variables in the same shell as every `git commit`, then verify with `git log -1 --format='%an <%ae> | %cn <%ce>'`.

## Working practice

- Prefer focused tests (one framework, a `--filter`, a test area or one `--verify-<area>` selector) over the full suites, which are slow.
- Report results honestly: what ran, what failed, what was not verified.
- When public usage changes, update the docs (both editions) and `skills/luminpack`.

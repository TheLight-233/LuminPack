# Repository constraints

Read [the mechanism contract](docs/serialization-mechanism-contract.md) and [CONTRIBUTING](CONTRIBUTING.md) before changing production or generator code. They apply to the entire repository, including generated code, runtime helpers, mapping, and conditional targets.

Preserve every established mechanism, not only wire compatibility: Local + As storage views, virtual Union dispatch, generic Union virtual plus cached-slot dispatch, version-tolerant length tables, reference tracking, value/unmanaged paths, constructor/pool/reuse behavior, callbacks, specialized formatters, state ownership, and all remaining mechanisms in the contract. Do not substitute another design without explicit user authorization. Record approved differences and distinguish correctness fixes from compatibility refactors.

Use English for code, comments, XML, diagnostics, tests, and maintenance documents; README is the exception. Do not remove storage placeholders or externally usable/generated members based only on ordinary call-site searches. Run the relevant structural, historical, runtime, and incremental checks specified by the contract.

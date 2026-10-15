# Architecture and maintenance boundaries

## Runtime and generator

`src/LuminPack` contains the binary and JSON readers and writers, size evaluation, operation state, collections, buffers, compression, cryptography, and mapping runtime. Unsafe storage views remain near the code that depends on their layout. Their field sequence and conditional layout are compatibility-sensitive.

`src/LuminPackGenerator` targets .NET Standard 2.0 and retains its existing Roslyn package compatibility. It analyzes consuming compilations and produces concrete formatter extensions, serializer overloads, union dispatch, layout mirrors, and mapper registration.

The runtime package depends on the separately packaged analyzer. The runtime does not execute that analyzer against itself. Each assembly owns an exception helper so neither needs an inverted dependency solely for throwing errors.

## Generator organization

| Component | Responsibility |
| --- | --- |
| `LuminPackSourceGenerator` | Registers incremental inputs and outputs. |
| `LuminPackSourceGenerator.Metadata` | Builds the ordered serialization model inside an isolated analysis scope. |
| `LuminPackSourceGenerator.Fields` | Analyzes member types, nested shapes, and inherited storage. |
| `LuminPackSourceGenerator.Constructors` | Selects and validates construction strategies. |
| `LuminPackSourceGenerator.TypeAnalysis` | Classifies symbols and discovers attributes and backing storage. |
| `LuminPackSourceGenerator.Unions` | Discovers union members and language union cases. |
| `LuminPackCodeGenerator.Serialization` / `Deserialization` | Emits binary field operations. |
| `LuminPackCodeGenerator.Evaluation` / `FieldSizes` | Emits size calculations and fixed-size expressions. |
| `LuminPackCodeGenerator.TypeNames` / `Unmanaged` | Resolves generated names and groups compatible unmanaged operations. |
| `LuminPackExtensionGenerator` parts | Builds formatter extensions, JSON support, union support, and dispatch. |
| `LuminPackSerializerGenerator` parts | Builds concrete and generic buffer, span, stream, JSON, compression, and size APIs. |
| `CodeEmitters` | Implements scalar and collection-specific emission; independent emitters have independent files. |
| `Mapping` | Separates binding analysis, automatic/manual mapping analysis, generation, and metadata models. |

Partial files preserve existing type identities and member signatures. They are organizational boundaries, not new runtime dispatch layers.

## Documentation generation

`GeneratedSourceDocumentation` fills missing XML comments in emitted methods by inserting comment text at declaration boundaries. Executable tokens are retained.

Documentation parsing is enabled locally even when the consuming compilation uses `DocumentationMode.None`. This preserves existing comments and supplies the same rich overload documentation for ordinary builds, IDE parsing, and XML-documentation builds. The regression runs all three documentation modes. A consuming project can enable `GenerateDocumentationFile` when it needs to export the generated member documentation with its assembly.

`LuminPackSerializerDocumentation` adds overload-specific type and member details. It reuses the packable metadata analysis, so the documented contract follows the formatter's member filtering and ordering instead of independently guessing from public reflection shape. Per-type XML fragments are reused across overloads within one generation call.

The generated documentation distinguishes:

- CLR layout kind, explicit offsets, pack and size attributes when present.
- Unmanaged storage copying, including padding and otherwise ignored storage fields.
- Ordinary binary member order and variable-length or reference framing.
- JSON's filtered named members, which need not match copied unmanaged storage.
- Custom formatter-owned payloads and generic formatter dispatch.
- Caller-owned buffers and streams, operation state, merge/reuse behavior, and encoded-byte return units.

Type and member links use compiler documentation identifiers. Generic and asynchronous overloads retain their own parameter and return-value contracts.

## Exception boundary

Production throws are constructed in `LuminPackExceptionHelper`. Generated buffer-writing catch paths reset the buffer and rethrow through `ExceptionDispatchInfo` in the helper. The existing `finally` cleanup stays in place. This keeps validation failures centralized without changing successful serialization paths.

## Verification

`tools/LuminPack.RepositoryChecks` parses .NET Standard, modern .NET, and Unity conditional branches. It checks explicit method XML, XML syntax and parameter coverage, English repository prose, and production/generated throw locations.

`tests/LuminPackDocumentationTest` compiles generated output using SDK Roslyn and validates documentation against representative serialization contracts. `tests/LuminPackGeneratorConcurrencyTest` retains the generator's concurrent-compilation and emitter-shape checks. The xUnit suite and wire fixture harness cover runtime behavior and persisted-data compatibility.

See [CONTRIBUTING](../CONTRIBUTING.md) for commands and review requirements.

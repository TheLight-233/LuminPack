# Contributing to LuminPack

## Building and checking changes

The solution includes .NET 11 targets and SDK-based documentation checks. Use an SDK capable of building those targets. `global.json` allows the installed preview SDK and rolls forward from a valid .NET 10 SDK feature band. Package consumers can still use the supported runtime target frameworks independently.

Run the checks from the repository root:

```text
dotnet build LuminPack.sln -c Release
dotnet run --project tools/LuminPack.RepositoryChecks/LuminPack.RepositoryChecks.csproj -c Release -- .
dotnet run --project tests/LuminPackDocumentationTest/LuminPackDocumentationTest.csproj -c Release
dotnet run --project tests/LuminPackGeneratorConcurrencyTest/LuminPackGeneratorConcurrencyTest.csproj -c Release
dotnet test tests/LuminPackUnitTest/LuminPackUnitTest.csproj -c Release
```

The documentation test uses the installed SDK's Roslyn assemblies so it can compile generated code using current language features. The existing concurrency and emitter-shape regressions continue to exercise the generator's older Roslyn dependency separately.

## Source conventions

- Follow `.editorconfig`: four spaces for C#, UTF-8 text, and consistent line endings. `.gitattributes` retains binary/archive and generated-artifact classification.
- Use English for implementation comments, XML documentation, diagnostics, examples, and maintenance documents. README files are the language exception. Represent multilingual test fixtures with Unicode escapes while preserving their actual values.
- Document every explicitly declared method, constructor, operator, and conversion with XML comments. Include meaningful summaries, parameter descriptions, generic type parameters, and return values. Use ordinary comments for local functions and lambdas, whose documentation is not emitted as member XML by the compiler.
- Place XML comments before attributes. Keep one summary per declaration, and avoid repeating type summaries on every part of a partial class.
- Use `<paramref>`, `<typeparamref>`, `<see>`, and `<seealso>` where they help readers navigate the API. Escape angle brackets in prose and examples.
- Keep unrelated emitters and mapping models in separate files. Use partial files to organize large generators by a clear responsibility while preserving their public type and member names.
- Use descriptive local names. Preserve public names, including established spellings, unless an explicitly reviewed compatibility change authorizes a rename.

Both library projects emit XML documentation files. Missing public documentation, invalid XML, mismatched parameter names, unresolved references, and misplaced documentation are compiler errors for those projects. Repository checks extend method coverage to private methods, tests, benchmarks, and conditional branches.

## Exceptions

Runtime exceptions belong in `LuminPack.Code.LuminPackExceptionHelper`. The generator has its own internal `LuminPack.SourceGenerator.LuminPackExceptionHelper`, keeping the analyzer independent of the runtime assembly. Generated runtime code must call the public runtime helper.

Preserve exception types, argument names, and cleanup behavior when moving a throw. Use `Rethrow` after cleanup when an existing exception must escape with its original identity and throwing stack. Do not replace it with a newly constructed wrapper or `throw exception`.

Tests may deliberately throw caller exceptions or assertion failures. Those fixtures establish behavior at library boundaries and are distinct from production exception policy.

## Serialization and layout compatibility

Changes to names, member order, enum values, null markers, string modes, framing, and unsafe storage views can affect persisted data. Public members that have no call sites in this repository can still be used by package consumers or generated code.

Remove private code only after checking references, generated references, callbacks, reflection use, and conditional compilation. Layout mirror fields, padding members, and storage placeholders can be necessary even when no ordinary field access references them.

For a compatibility refactor, capture wire fixtures with the original build, verify them with the updated build, and compare newly written fixture bytes:

```text
dotnet run --project tests/LuminPackWireCompatibility/LuminPackWireCompatibility.csproj -c Release -- write .tmp/wire-baseline
dotnet run --project tests/LuminPackWireCompatibility/LuminPackWireCompatibility.csproj -c Release -- verify .tmp/wire-baseline
```

The wire harness references the Release runtime and generator outputs; build them before running it. Keep temporary fixtures and comparison reports under `.tmp` rather than committing machine-specific artifacts.

Treat `Unmanaged` source files and their `.tt` templates as a pair. Keep comments and generated APIs consistent. Preserve third-party notices and licenses when editing the hash and floating-point formatting ports.

## Reviewing generated overload documentation

Concrete serializer overloads must identify the exact type and actual filtered member contract. Obtain those details from the same metadata analysis used for formatter generation, including explicit ordering and private-member inclusion.

Keep CLR storage layout distinct from binary member order and JSON property contracts. Unmanaged copies include storage padding; variable-length members and reference headers do not have a universal compile-time offset. Custom formatters own their representation. Generic fallbacks must describe formatter dispatch rather than claim a concrete layout.

When changing documentation generation, run the documentation regression. It compiles representative models and checks type links, member order, Include/Ignore behavior, explicit offsets, unmanaged storage versus JSON fields, generation modes, asynchronous overloads, and encoded-byte return units.

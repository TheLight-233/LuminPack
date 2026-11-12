# Next generator

[Chinese](generator-next.cn.md)

`src/LuminPack.Generator.Next` is the model-based generator. The benchmark and runtime test projects select it with `-p:LuminPackGenerator=Next`; their legacy selection remains available for comparisons. The packaged generator is a separate project.

## Output layout

One incremental generator publishes three directories:

| Directory | Contents |
| --- | --- |
| `Binary` | Binary facade, payload algorithms, Union dispatch and intercepted entries. |
| `Json` | JSON facade, payload algorithms and JSON Union dispatch. |
| `Formatters` | Shared framework formatters, compiled profiles and registration support. |

Filenames follow the established generator's `LuminPackGen.<Namespace>.<Name>.g.cs` convention. Generic arguments become their declaration's arity. Built-in scalars share `LuminPackGen.System.g.cs`, arrays share `LuminPackGen.Array.g.cs`, and closed constructions of a framework generic definition share one file.

Core algorithms precede necessary adapters. Retained serialization convenience APIs appear at the end of each payload file. Empty generated extension containers and their empty namespace fragments are omitted. File-local aliases identify static writer and reader owners. JSON operations have a `Json` suffix. Pure generated forwarding methods, including `WritePolymorphismValue` and `DeserializeUnion`, are removed after their callers are redirected. Compiler interception and typed delegate adapters retain their required signatures. Generated implementation names are not application entry points.

Ordinary records and their Union payloads share algorithms with stateless framing type arguments. `LuminPackObjectFraming` and `LuminPackPayloadFraming` live in Core. Their closed instantiations retain their own null/header handling, reservations, checked reads and publication boundaries. Wrappers that copy reader or writer state remain separate where failures must preserve the caller's cursor.

Union writing propagates compiled options through one generic virtual method per format. Binary writes additionally retain one non-generic standard-profile slot so the common path avoids generic virtual dispatch overhead. Closed generic cases keep their cached dispatch slots. A Union reader uses one case switch for fresh and existing targets; each case determines whether the current instance has the exact required type.

## Owner structs and shared generic formatters

Generated formatter owners are field-free `readonly` structs with static operations. Core declares one interface per operation: `ILuminPackWriter<T>`, `ILuminPackReader<T>`, `ILuminPackJsonWriter<T>` and `ILuminPackJsonReader<T>`. Their members are instance members on every target, and shared code calls them as `default(TFormatter).Write<TOptions>(...)`. The call binds to the exact owner of each instantiation without boxing or dispatch, and the empty receiver disappears once the forwarder is inlined. Static abstract members are not used: NativeAOT cannot compile static virtual calls in generic code shared over reference-type elements.

Closed instantiations of every registered sequence and keyed family (for example `List<T>`, `HashSet<T>`, `Queue<T>`, `Stack<T>`, `SortedSet<T>`, `ImmutableArray<T>`, `IList<T>`, `Dictionary<TKey, TValue>`, `SortedDictionary<TKey, TValue>`, `ConcurrentDictionary<TKey, TValue>`) and arrays share one generic owner per family, array rank and element-category combination. A raw element uses the family's scalar template and any other element its general element template, as the closed publisher does. Each argument is classified:

| Category | Rule | Generic form |
| --- | --- | --- |
| Unmanaged | The closed owner copies its storage (raw scalars, plain unmanaged structs, `Native` layouts) | Type parameter with `unmanaged`; JSON operations add a strategy parameter |
| String | `string` | Stays literal; the string kernel is unchanged |
| Element | Any other non-polymorphic element with its own formatter, including `Tagged` and `Packed` structs and arrays | Type parameter and a `struct` strategy parameter implemented by the element owner |

`Dictionary<int, int>` and `Dictionary<long, double>` share `DictionaryOfUnmanagedAndUnmanagedFormatter<TKey, TValue, ...>`, `Dictionary<int, string>` uses `DictionaryOfUnmanagedAndStringFormatter<TKey, ...>`, and `Dictionary<string, SimpleClass>` uses `DictionaryOfStringAndElementFormatter<TValue, TValueFormatter>`. Nested collections are elements: `Dictionary<int, List<string>>` passes the `List<string>` owner as its strategy.

A collection owns only its container structure; the element's layout and encoding stay in the element's own owner:

- A `Tagged` or `Packed` element of a contiguous collection keeps its block transfer. Core's `ILuminPackBlockWriter<T>` and `ILuminPackBlockReader<T>` expose the block width, validation, write and read, and the element owner implements them with the closed transfer: the layout descriptors, the vectorized prefix and the scalar tail. `List<TagA>` and `TagB[]` therefore share `ListOfLayoutFormatter<T, TFormatter>` and `ArrayOfLayoutFormatter<T, TFormatter>`. A template that spells a layout's packed width keeps it in the shared render, so different widths get different shared owners.
- A nested array is an element whose strategy is the inner array's owner, which is itself shared. `int[][]` and `long[][]` share `ArrayOfElement2Formatter<T, TFormatter, TCompressedFormatter>`. The compressed roots of an array or list of arrays call a second strategy, `IntArrayCompressedFormatter`, which forwards to the inner array's compressed entries.
- A rectangular array shares the row-major template of its rank: `int[,]` and `long[,]` use `Array2DOfUnmanagedFormatter<T, TFormatter>`, and `SimpleClass[,,]` uses `Array3DOfElementFormatter<T, TFormatter>`. Raw blocks, layout blocks and element loops keep their closed rules.

Union, interface, `object`, Boolean and nullable elements keep their specialized closed owners, so polymorphic dispatch and Boolean validation are unchanged. Laid-out elements are shared only when Core declares the block interfaces.

The generic owner is rendered by the family's own template with placeholder type names, so its algorithm is the closed algorithm. Closed owners share it only when their placeholder renders and operation sets are identical; differing renders receive numbered names. A closed owner keeps its state extensions, and its roots become forwarders that publication resolves to a file alias such as `using DictionaryOfIntAndIntWriter = ...DictionaryOfUnmanagedAndUnmanagedFormatter<int, int, IntFormatter, IntFormatter>;`. Call sites keep their spelling. Strategy members, published in `LuminPackGen.FormatterStrategies.g.cs`, make exactly the element call the closed owner made. Owners whose entries or extensions need roots the shared owner lacks and families that forward to another closed owner, such as `IDictionary<TKey, TValue>`, stay closed. A shape used by only one closed owner also stays closed.

Every root of a shared owner is `NoInlining`. One call into a collection keeps the caller's inline budget for the element calls inside its loop; an inlined collection root would copy the loop into each parent and leave the element calls uninlined. Strategy forwarders and payload record writes remain inlining candidates.

## Registering external Union cases

Register through the serializer:

```csharp
LuminPackSerializer.Register<IMessage, ExternalMessage>(
    77, WriteMessage, ReadMessage, WriteMessageJson, ReadMessageJson);
```

The binary writer takes `ref LuminPackWriter, ref IMessage`; the binary reader takes `ref LuminPackReader, ref IMessage`. JSON delegates use the corresponding JSON state. All four operations are required. Writers publish the complete Union tag/envelope; readers consume the case payload at the existing dispatch boundary.

The member must implement or derive from the root. Tags must fit in `UInt16`; generated tags, duplicate tags and duplicate member registrations are rejected. Core's hidden `LuminPackUnionFormatter<TUnion>` owns the shared table and generated tag reservations, so roots no longer publish individual registration APIs. Ordinary custom formatter registration continues to use the existing typed `LuminPackSerializer.Register<T>` overloads.

## Incremental behavior and verification

Declarations use Roslyn's attribute-indexed incremental provider. Assembly settings and bound serializer calls enter as separate value-compared inputs. Driver-owned snapshots retain compiler-free model facts, declarations and final files; an unchanged document reuses its parsed method references and exact operation dependencies. The publication pass consumes source-writer declaration offsets instead of creating a second compilation or binding generated code.

Transient source construction uses exclusive, bounded array-pool leases. Span operations fill and copy validated character ranges; statement alignment also rents its temporary integer table. Success, failure and cancellation dispose the leases. Oversized buffers are not retained in the pool, and published strings own their character storage. The final strings, normalized models and Roslyn syntax still allocate; the generator is not allocation-free. Publication hot loops use explicit iteration instead of LINQ pipelines.

```text
dotnet run --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -c Release -- --verify-publication
dotnet run --project src/LuminPack.Generator.Next/benchmarks/GeneratorBenchmarks -c Release -- --types 256 --runs 9 --warmups 8 --old-dll <reference-generator.dll> --old-mode off
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-roundtrip
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -- --verify-fixtures
```

The reference DLL can be an earlier Next build or the legacy generator. A comparison warms both generators, alternates their execution order and prints every timing sample alongside the median, full range and median allocations. Use --reference-first to reverse the initial order. Use --verify-equivalence with a frozen build of the same output architecture to compare filenames, source text and diagnostics after full generation, comment/member edits, deletion and restoration. Compare complete generation and both incremental edits, generated text, allocations, wire fixtures and runtime measurements. Source reduction alone does not establish runtime or generator performance.

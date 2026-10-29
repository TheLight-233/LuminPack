<div align="center">

# ✨ LuminPack

**Blazing-fast binary & JSON serialization for .NET and Unity**

*Zero reflection · Zero-allocation reads · Ready for IL2CPP and NativeAOT*

[![NuGet](https://img.shields.io/nuget/v/LuminPack.svg?style=flat-square&logo=nuget&label=NuGet)](https://www.nuget.org/packages/LuminPack)
[![Downloads](https://img.shields.io/nuget/dt/LuminPack.svg?style=flat-square&logo=nuget&label=Downloads)](https://www.nuget.org/packages/LuminPack)
[![License](https://img.shields.io/github/license/TheLight-233/LuminPack?style=flat-square&label=License)](LICENSE)
[![Stars](https://img.shields.io/github/stars/TheLight-233/LuminPack?style=flat-square&logo=github&label=Stars)](https://github.com/TheLight-233/LuminPack/stargazers)

![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010%20%7C%2011-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.1-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Unity](https://img.shields.io/badge/Unity-Mono%20%7C%20IL2CPP-000000?style=flat-square&logo=unity&logoColor=white)
![NativeAOT](https://img.shields.io/badge/NativeAOT-ready-2EA043?style=flat-square)

**English** · [简体中文](README.cn.md)

[Quick Start](#-quick-start) ·
[Features](#-features) ·
[Documentation](#-documentation) ·
[Benchmarks](#-benchmarks) ·
[Platform Packages](#-platform-packages)

</div>

---

**LuminPack** is a high-performance serializer for C#. Mark a type with `[LuminPackable]` and a source generator writes a dedicated binary and JSON serializer for it at compile time — no reflection, no runtime code generation, no hidden allocations. It was built for game saves, network packets and real-time systems where every microsecond and every byte counts, and it runs everywhere .NET does: from .NET 11 servers down to Unity IL2CPP on consoles and phones.

```csharp
[LuminPackable]
public partial class Player
{
    public int Id;
    public string Name = "";
    public List<int> Scores = new();
}

byte[] bytes = LuminPackSerializer.Serialize(player);       // 🚀 binary
LuminPackSerializer.Deserialize(bytes, ref player);          // ♻️ refills the same object, zero allocation
string json = LuminPackSerializer.SerializeJson(player);     // 📜 JSON from the same model
```

## 🌟 Features

<table>
<tr>
<td width="50%" valign="top">

### ⚡ Built for speed
Every type gets hand-tuned-quality serialization code generated at compile time. Plain structs are copied straight from memory, strings take vectorized fast paths, and the hot path never touches reflection or dictionaries.

</td>
<td width="50%" valign="top">

### ♻️ Zero-allocation reads
Deserialize into the objects you already have. Lists, arrays, dictionaries, nested objects and unchanged strings are reused, so a warmed-up read can allocate nothing at all.

</td>
</tr>
<tr>
<td valign="top">

### 📦 Binary and JSON, one model
Two complete formats from a single annotation: a compact binary format for storage and the wire, and standard JSON (UTF-8 or UTF-16) for configs, tools and web APIs.

</td>
<td valign="top">

### 📝 Version tolerance
Add, remove, reorder and rename fields without breaking old saves. Old and new versions of your app read each other's data.

</td>
</tr>
<tr>
<td valign="top">

### 🎭 Polymorphism done right
Interfaces and abstract classes just work: subclasses are discovered automatically, generic cases and types from other assemblies are supported, and C# 15 `union` types are recognized on .NET 11.

</td>
<td valign="top">

### 🔗 Real object graphs
Shared references and cycles, private fields, constructors, callbacks and your own instance pools — LuminPack handles the models you actually write.

</td>
</tr>
<tr>
<td valign="top">

### 🎮 Unity and AOT first
Runs on Unity Mono and IL2CPP, NativeAOT and full trimming. Optional packages add Unity value types and a Burst-accelerated backend.

</td>
<td valign="top">

### 🔧 Configured at compile time
Choose string encodings, JSON limits, portable 32/64-bit struct layouts or a hardened mode for untrusted input. Whatever you don't turn on costs nothing.

</td>
</tr>
<tr>
<td valign="top">

### 🪶 Small builds
Generation modes trim the generated code down to the types you really serialize, keeping large projects fast to compile.

</td>
<td valign="top">

### 🌐 Modern I/O
`Span<T>`, `ReadOnlySequence<byte>`, `IBufferWriter<byte>`, pipes and async streams are all first-class.

</td>
</tr>
</table>

## 📦 Installation

```shell
dotnet add package LuminPack
```

```xml
<PackageReference Include="LuminPack" Version="2.0.0" />
```

[`LuminPack`](https://www.nuget.org/packages/LuminPack) brings the matching source generator with it. Targets `netstandard2.1` and `net8.0` – `net11.0`.

> 🎮 **Unity?** Set the API compatibility level to .NET Standard 2.1 and follow the [Unity guide](docs/unity.md).

## 🚀 Quick Start

```csharp
using LuminPack;
using LuminPack.Attribute;

[LuminPackable]
public partial class Person
{
    public int Age { get; set; }
    public string Name { get; set; } = "";
}

var person = new Person { Age = 18, Name = "Light" };

// Binary
byte[] bytes = LuminPackSerializer.Serialize(person);
Person copy = LuminPackSerializer.Deserialize<Person>(bytes);

// JSON
string json = LuminPackSerializer.SerializeJson(person);
Person fromJson = LuminPackSerializer.DeserializeJson<Person>(json);
```

That's it — `LuminPackSerializer` is generated into your project with overloads for every type you serialize. See [Getting Started](docs/getting-started.md) for buffers, streams and `IBufferWriter<byte>`.

## 🧭 A Quick Tour

<details>
<summary><b>🎭 Polymorphism</b></summary>

```csharp
[LuminPackable]
public abstract partial class Shape { }

[LuminPackable] public sealed partial class Circle : Shape { public float Radius; }
[LuminPackable] public sealed partial class Square : Shape { public float Side; }

Shape shape = new Circle { Radius = 2 };
byte[] bytes = LuminPackSerializer.Serialize(shape);
Shape back = LuminPackSerializer.Deserialize<Shape>(bytes);   // Circle
```

➡️ [Polymorphism guide](docs/polymorphism.md)
</details>

<details>
<summary><b>📝 Version tolerance</b></summary>

```csharp
[LuminPackable(GeneratorType.VersionTolerant)]
public partial class SaveData
{
    public int Level;

    [LuminPackOldName("Name")]          // renamed from Name, old saves still load
    public string DisplayName = "";

    public List<int> Unlocks = new();   // added later
}
```

➡️ [Version tolerance guide](docs/versioning.md)
</details>

<details>
<summary><b>♻️ Zero-allocation reuse and pooling</b></summary>

```csharp
var state = new WorldState();
while (running)
    LuminPackSerializer.Deserialize(ReceivePacket(), ref state);   // same objects every frame

[LuminPackable]
public partial class Bullet
{
    public float X, Y;

    [LuminPackPoolRent]
    public static Bullet Rent() => BulletPool.Rent();               // new instances come from your pool
}
```

➡️ [Reuse guide](docs/reuse.md)
</details>

<details>
<summary><b>🔗 Shared references and cycles</b></summary>

```csharp
[LuminPackable(GeneratorType.CircleReference)]
public partial class Node
{
    [LuminPackOrder(0)] public Node? Parent;
    [LuminPackOrder(1)] public List<Node>? Children;
}
```

➡️ [Reference tracking](docs/versioning.md#reference-tracking)
</details>

<details>
<summary><b>🔧 Options and portable structs</b></summary>

```csharp
[assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8)]

[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public partial struct Packet            // identical bytes on 32-bit and 64-bit
{
    public byte Kind;
    public long Sequence;
}
```

➡️ [Options guide](docs/options.md)
</details>

## 📊 Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org) on the repository's CI runner — **lower is better**. Every chart compares libraries on the same machine, data and run; the multiplier shows how long each library takes relative to LuminPack, and the whiskers show the min–max spread.

| 🖥️ Test machine | |
| --- | --- |
| Runner | GitHub Actions `ubuntu-latest` (`Benchmark (linux-x64)` job of the [CI workflow](.github/workflows/ci.yml)) |
| CPU | AMD EPYC 7763 2.67 GHz · 2 cores / 4 threads |
| OS | Ubuntu 24.04.5 LTS |
| Runtime | .NET 10.0.14 · X64 RyuJIT (x86-64-v3 / AVX2) |
| Tooling | BenchmarkDotNet 0.15.6 · .NET SDK 11.0.100-rc.3 |
| Settings | 3 launches × 15 iterations, 6 warmups, Server GC for all four benchmark classes |

### 🎮 Game save

A realistic save file with nested objects, lists, dictionaries and strings.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/benchmarks/SerializeBenchmark-dark.svg">
  <img alt="Game save benchmark" src="docs/assets/benchmarks/SerializeBenchmark-light.svg" width="100%">
</picture>

### 🧱 Simple class

A small class with numbers, an array, a list and a dictionary — the classic micro-benchmark shape.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/benchmarks/SimpleBenchmark-dark.svg">
  <img alt="Simple class benchmark" src="docs/assets/benchmarks/SimpleBenchmark-light.svg" width="100%">
</picture>

### 🎭 Polymorphism

Interfaces and abstract classes with several concrete cases.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/benchmarks/PolymorphismBenchmark-dark.svg">
  <img alt="Polymorphism benchmark" src="docs/assets/benchmarks/PolymorphismBenchmark-light.svg" width="100%">
</picture>

### 📜 JSON

The same game save as JSON text, against the most popular .NET JSON libraries.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/assets/benchmarks/JsonBenchmark-dark.svg">
  <img alt="JSON benchmark" src="docs/assets/benchmarks/JsonBenchmark-light.svg" width="100%">
</picture>

Want to see it on your own hardware? Run the same four benchmark classes locally, or dispatch the CI workflow with `run_benchmarks` enabled to get fresh results in the per-host `benchmark-artifacts-*` uploads:

```shell
dotnet run --project benchmarks/LuminPack.Benchmark -c Release -f net10.0 -- --filter "LuminPackBenchmark.SimpleBenchmark.*" "LuminPackBenchmark.SerializeBenchmark.*" "LuminPackBenchmark.JsonBenchmark.*" "LuminPackBenchmark.PolymorphismBenchmark.*"
```

## 📚 Documentation

| | Guide | You'll learn |
| :---: | --- | --- |
| 🚀 | [Getting Started](docs/getting-started.md) | Installation, the first type, every entry point, buffers and streams |
| 🎯 | [Defining Types](docs/defining-types.md) | Members, ordering, constructors, callbacks, size guarantees, supported types |
| 🎭 | [Polymorphism](docs/polymorphism.md) | Unions, generic cases, types from other assemblies, manual registration |
| 📝 | [Version Tolerance & References](docs/versioning.md) | Evolving schemas, renamed members, shared objects and cycles |
| ♻️ | [Reuse & Zero Allocation](docs/reuse.md) | Reading into existing objects, instance pools, output buffers |
| 🔧 | [Compile-time Options](docs/options.md) | String encodings, option profiles, JSON limits, 32/64-bit struct layouts |
| 🪶 | [Generation Modes](docs/generation-modes.md) | `Full`, `Medium`, `Light`, `Minimal` |
| ✍️ | [Custom Formatters](docs/custom-formatters.md) | Hand-written formats, `Register<T>`, low-level reader and writer |
| 🎮 | [Unity, IL2CPP & NativeAOT](docs/unity.md) | Unity setup, Burst, trimming |
| 📐 | [Wire Formats](docs/formats.md) | The binary layout and the JSON representation |
| 🛡️ | [Security](docs/security.md) | Portability, untrusted input, encryption |

## 🧩 Platform Packages

Optional add-ons with ready-made support for engine and UI types. The core library depends on none of them.

| Package | Adds support for |
| --- | --- |
| 🎮 [LuminPack.Unity](src/LuminPack.Unity/README.md) | Vectors, colors, bounds, matrices, curves, gradients |
| 🔥 [LuminPack.Burst](src/LuminPack.Burst/README.md) | Burst-accelerated kernels for Unity |
| 🌐 [LuminPack.AspNetCore](src/LuminPack.AspNetCore/README.md) | MVC and Minimal API binary bodies, HTTP value types |
| 🖼️ [LuminPack.Avalonia](src/LuminPack.Avalonia/README.md) | Geometry, colors, matrices |
| 🎨 [LuminPack.Drawing](src/LuminPack.Drawing/README.md) | `System.Drawing.Color` |
| 📱 [LuminPack.Maui](src/LuminPack.Maui/README.md) | MAUI Graphics geometry and colors |
| 🕹️ [LuminPack.MonoGame](src/LuminPack.MonoGame/README.md) | Vectors, matrices, bounding volumes |
| 🖌️ [LuminPack.SkiaSharp](src/LuminPack.SkiaSharp/README.md) | Geometry, colors, matrices |
| 🪟 [LuminPack.Wpf](src/LuminPack.Wpf/README.md) | Geometry, 3D types, colors |

## 🤖 AI Skill

Using an AI coding assistant? [`skills/luminpack`](skills/luminpack/SKILL.md) teaches it to write correct LuminPack code — attributes, APIs, Unions, version tolerance, custom formatters and the common pitfalls. Drop the folder into your assistant's skill directory (for example `.claude/skills/`) and you're set.

## 🤝 Contributing

Issues and pull requests are welcome! Please read the [contributing guide](CONTRIBUTING.md) first; the [architecture overview](docs/architecture.md) and the [design rules](docs/mechanism-contract.md) explain how the library fits together.

## 💖 Acknowledgements

LuminPack learned a great deal from the design of [MemoryPack](https://github.com/Cysharp/MemoryPack) in its early days and has since grown in its own direction. Floating-point JSON formatting is ported from [xjb](docs/xjb-NOTICE.md).

## 📄 License

LuminPack is released under the [MIT License](LICENSE). Third-party notices: [xjb license](docs/xjb-LICENSE.txt) (Apache 2.0) · [xjb notice](docs/xjb-NOTICE.md).

<div align="center">

**If LuminPack helps you, a ⭐ on GitHub means a lot!**

</div>

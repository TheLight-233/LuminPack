```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=12  
IterationTime=250ms  WarmupCount=5  

```
| Method   | Length | Mean         | Error      | StdDev     | Ratio | Allocated | Alloc Ratio |
|--------- |------- |-------------:|-----------:|-----------:|------:|----------:|------------:|
| **Baseline** | **8**      |     **42.23 ns** |   **0.525 ns** |   **0.410 ns** |  **1.00** |         **-** |          **NA** |
| Runtime  | 8      |     31.93 ns |   0.359 ns |   0.280 ns |  0.76 |         - |          NA |
|          |        |              |            |            |       |           |             |
| **Baseline** | **4096**   | **18,506.90 ns** | **155.583 ns** | **112.496 ns** |  **1.00** |         **-** |          **NA** |
| Runtime  | 4096   | 13,361.63 ns |  88.329 ns |  63.867 ns |  0.72 |         - |          NA |

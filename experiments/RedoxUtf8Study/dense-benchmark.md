```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=12  
IterationTime=250ms  WarmupCount=5  

```
| Method   | Length | Mean         | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------- |------- |-------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **Baseline** | **8**      |     **42.51 ns** |   **0.472 ns** |   **0.368 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Runtime  | 8      |     44.71 ns |   0.600 ns |   0.468 ns |  1.05 |    0.01 |         - |          NA |
|          |        |              |            |            |       |         |           |             |
| **Baseline** | **4096**   | **18,914.72 ns** | **335.668 ns** | **222.024 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Runtime  | 4096   | 19,813.43 ns | 159.111 ns | 115.048 ns |  1.05 |    0.01 |         - |          NA |

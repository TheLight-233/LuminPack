```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  Job-NRROAN : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Affinity=00000000000000000011  Runtime=.NET 10.0  IterationCount=12  
IterationTime=250ms  LaunchCount=1  WarmupCount=5  

```
| Method             | Length | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------- |------- |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **AllocateAndFlatten** | **1024**   |   **103.04 ns** |   **8.684 ns** |  **6.780 ns** |  **1.00** |    **0.09** | **0.0839** |    **1056 B** |        **1.00** |
| StagedSequence     | 1024   |    58.60 ns |   0.289 ns |  0.225 ns |  0.57 |    0.04 |      - |         - |        0.00 |
|                    |        |             |            |           |       |         |        |           |             |
| **AllocateAndFlatten** | **65536**  | **4,881.87 ns** | **111.792 ns** | **73.944 ns** |  **1.00** |    **0.02** | **5.2067** |   **65568 B** |        **1.00** |
| StagedSequence     | 65536  | 2,295.32 ns |  86.871 ns | 67.823 ns |  0.47 |    0.02 |      - |         - |        0.00 |

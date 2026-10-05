```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  Job-NRROAN : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Affinity=00000000000000000011  Runtime=.NET 10.0  IterationCount=12  
IterationTime=250ms  LaunchCount=1  WarmupCount=5  

```
| Method       | Length | Mean        | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------- |------- |------------:|---------:|---------:|------:|--------:|----------:|------------:|
| **BufferedCopy** | **16**     |    **352.6 ns** | **13.35 ns** | **10.42 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| DirectOutput | 16     |    361.2 ns | 14.00 ns | 10.93 ns |  1.03 |    0.04 |         - |          NA |
|              |        |             |          |          |       |         |           |             |
| **BufferedCopy** | **30000**  | **24,921.8 ns** | **44.11 ns** | **31.89 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| DirectOutput | 30000  | 23,396.6 ns | 39.91 ns | 31.16 ns |  0.94 |    0.00 |         - |          NA |

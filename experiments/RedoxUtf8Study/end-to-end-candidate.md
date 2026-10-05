```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 11.0.100-preview.7.26381.103
  [Host]             : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  ShortRun-.NET 10.0 : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 10.0  Affinity=00000000000000000011  Runtime=.NET 10.0  
Server=False  IterationCount=12  IterationTime=250ms  
LaunchCount=1  WarmupCount=5  

```
| Method                       | Mean     | Min      | Max      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|------:|-------:|-------:|----------:|------------:|
| LuminPackDeserializeFromJson | 93.74 μs | 93.01 μs | 94.51 μs |  1.00 | 4.8653 | 0.3743 |   62441 B |        1.00 |
|                              |          |          |          |       |        |        |           |             |
| LuminPackSerializeToJson     | 29.14 μs | 29.06 μs | 29.22 μs |  1.00 |      - |      - |         - |          NA |

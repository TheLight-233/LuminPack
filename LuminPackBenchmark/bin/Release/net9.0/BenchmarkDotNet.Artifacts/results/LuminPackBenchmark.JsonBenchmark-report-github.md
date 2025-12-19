```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]             : .NET 9.0.3, X64 NativeAOT x86-64-v3
  ShortRun-.NET 10.0 : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 10.0  Runtime=.NET 10.0  Server=False  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean     | Min      | Max      | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------------- |---------:|---------:|---------:|------:|-------:|-------:|----------:|------------:|
| LuminPackSerializeToJson     | 16.84 μs | 16.82 μs | 16.87 μs |  1.00 |      - |      - |         - |          NA |
| SystemTextJsonSerialize      | 22.46 μs | 21.99 μs | 23.37 μs |  1.33 | 1.1292 |      - |   14272 B |          NA |
| NewtonsoftJsonSerialize      | 44.92 μs | 44.86 μs | 45.04 μs |  2.67 | 6.7139 | 0.3662 |   84464 B |          NA |
| LuminPackDeserializeFromJson | 17.19 μs | 17.09 μs | 17.24 μs |  1.02 | 0.3967 |      - |    5312 B |          NA |
| SystemTextJsonDeserialize    | 26.31 μs | 26.26 μs | 26.35 μs |  1.56 | 0.9460 |      - |   12016 B |          NA |
| NewtonsoftJsonDeserialize    | 63.58 μs | 63.34 μs | 63.88 μs |  3.78 | 4.3945 |      - |   56320 B |          NA |

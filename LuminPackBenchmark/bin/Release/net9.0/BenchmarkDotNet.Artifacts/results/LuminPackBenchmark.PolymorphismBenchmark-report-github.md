```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26100.6899/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]            : .NET 9.0.3, X64 NativeAOT x86-64-v3
  ShortRun-.NET 9.0 : .NET 9.0.3 (9.0.3, 9.0.325.11113), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 9.0  Runtime=.NET 9.0  Server=False  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                 | Mean       | Min        | Max        | Gen0   | Gen1   | Allocated |
|----------------------- |-----------:|-----------:|-----------:|-------:|-------:|----------:|
| LuminPackSerialize     |   5.201 μs |   5.144 μs |   5.308 μs |      - |      - |         - |
| MemoryPackSerialize    |  26.206 μs |  25.212 μs |  27.327 μs |      - |      - |   65536 B |
| MessagePackSerialize   |  85.844 μs |  85.510 μs |  86.210 μs | 5.2490 |      - |   67232 B |
| NinoSerialize          |  37.244 μs |  37.183 μs |  37.354 μs |      - |      - |         - |
| LuminPackDeserialize   |  11.080 μs |  10.752 μs |  11.351 μs | 6.0272 | 1.0681 |   75688 B |
| MemoryPackDeserialize  |  17.149 μs |  17.084 μs |  17.268 μs | 6.0120 | 1.0681 |   75688 B |
| MessagePackDeserialize | 120.778 μs | 120.083 μs | 121.165 μs | 6.3477 | 1.2207 |   79856 B |
| NinoDeserialize        |  15.581 μs |  15.507 μs |  15.672 μs | 6.0120 | 1.0681 |   75688 B |

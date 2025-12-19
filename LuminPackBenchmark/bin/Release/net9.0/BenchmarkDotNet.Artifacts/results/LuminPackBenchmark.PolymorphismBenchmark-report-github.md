```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7171/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]            : .NET 9.0.3, X64 NativeAOT x86-64-v3
  ShortRun-.NET 9.0 : .NET 9.0.3 (9.0.3, 9.0.325.11113), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 9.0  Runtime=.NET 9.0  Server=False  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                 | Mean       | Min        | Max        | Gen0   | Gen1   | Allocated |
|----------------------- |-----------:|-----------:|-----------:|-------:|-------:|----------:|
| LuminPackSerialize     |   6.171 μs |   5.935 μs |   6.356 μs |      - |      - |         - |
| MemoryPackSerialize    |  38.620 μs |  37.090 μs |  40.616 μs |      - |      - |   65536 B |
| MessagePackSerialize   | 120.513 μs | 114.967 μs | 124.788 μs | 5.2490 |      - |   67232 B |
| NinoSerialize          |  20.300 μs |  19.287 μs |  21.437 μs |      - |      - |         - |
| LuminPackDeserialize   |  15.928 μs |  15.309 μs |  16.619 μs | 6.0272 | 1.0681 |   75688 B |
| MemoryPackDeserialize  |  23.866 μs |  22.940 μs |  24.394 μs | 6.0120 | 1.0681 |   75688 B |
| MessagePackDeserialize | 165.327 μs | 161.724 μs | 172.515 μs | 6.3477 | 1.2207 |   79856 B |
| NinoDeserialize        |  15.700 μs |  15.667 μs |  15.721 μs | 6.0120 | 1.0681 |   75688 B |

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
| LuminPackSerialize     |   101.1 ns |   100.8 ns |   101.3 ns |      - |      - |         - |
| MemoryPackSerialize    |   825.7 ns |   773.4 ns |   874.2 ns |      - |      - |    2048 B |
| MessagePackSerialize   | 2,434.3 ns | 2,428.7 ns | 2,444.8 ns | 0.2251 |      - |    2832 B |
| NinoSerialize          |   791.2 ns |   787.5 ns |   794.5 ns |      - |      - |         - |
| LuminPackDeserialize   |   267.3 ns |   262.4 ns |   271.2 ns | 0.2427 | 0.0014 |    3048 B |
| MemoryPackDeserialize  |   343.5 ns |   338.9 ns |   352.3 ns | 0.2427 | 0.0014 |    3048 B |
| MessagePackDeserialize | 3,268.8 ns | 3,221.0 ns | 3,302.9 ns | 0.2403 |      - |    3048 B |
| NinoDeserialize        |   274.0 ns |   273.3 ns |   274.4 ns | 0.2427 | 0.0014 |    3048 B |

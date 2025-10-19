```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26100.6899/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]            : .NET 9.0.3, X64 NativeAOT x86-64-v3
  ShortRun-.NET 9.0 : .NET 9.0.3 (9.0.3, 9.0.325.11113), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 9.0  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method               | Mean       | Min        | Max        | Gen0   | Gen1   | Allocated |
|--------------------- |-----------:|-----------:|-----------:|-------:|-------:|----------:|
| LuminPackSerialize   |   409.9 ns |   408.0 ns |   411.1 ns |      - |      - |         - |
| MemoryPackSerialize  | 1,830.6 ns | 1,803.8 ns | 1,857.9 ns |      - |      - |         - |
| MessagePackSerialize | 2,587.4 ns | 2,566.3 ns | 2,603.2 ns | 0.2594 |      - |    3288 B |
| NinoSerialize        | 1,118.7 ns | 1,118.2 ns | 1,119.2 ns |      - |      - |         - |
| LuminPackDe          | 1,175.2 ns | 1,156.7 ns | 1,184.8 ns | 0.6828 | 0.0153 |    8568 B |
| MemoryPackDe         | 1,638.5 ns | 1,622.7 ns | 1,649.8 ns | 0.6828 | 0.0153 |    8568 B |
| MessagePackDe        | 4,523.2 ns | 4,461.0 ns | 4,572.0 ns | 1.0147 | 0.0305 |   12736 B |
| NinoDe               | 1,601.8 ns | 1,576.7 ns | 1,615.6 ns | 0.6828 | 0.0153 |    8568 B |

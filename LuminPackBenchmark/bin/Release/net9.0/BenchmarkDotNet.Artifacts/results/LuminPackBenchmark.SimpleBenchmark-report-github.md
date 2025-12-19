```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26100.7462/24H2/2024Update/HudsonValley)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
  [Host]             : .NET 9.0.3, X64 NativeAOT x86-64-v3
  ShortRun-.NET 10.0 : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3

Job=ShortRun-.NET 10.0  Runtime=.NET 10.0  Server=False  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                 | Mean        | Min         | Max         | Gen0   | Gen1   | Allocated |
|----------------------- |------------:|------------:|------------:|-------:|-------:|----------:|
| LuminPackSerialize     |    78.14 ns |    77.87 ns |    78.44 ns |      - |      - |         - |
| MemoryPackSerialize    |   280.41 ns |   279.71 ns |   280.88 ns |      - |      - |         - |
| MessagePackSerialize   | 1,639.53 ns | 1,634.75 ns | 1,643.30 ns | 0.2251 |      - |    2832 B |
| NinoSerialize          |   187.75 ns |   187.29 ns |   188.58 ns |      - |      - |         - |
| LuminPackDeserialize   |   250.47 ns |   246.07 ns |   255.00 ns | 0.2375 | 0.0038 |    2984 B |
| MemoryPackDeserialize  |   334.63 ns |   328.47 ns |   339.11 ns | 0.2427 | 0.0014 |    3048 B |
| MessagePackDeserialize | 3,141.48 ns | 3,121.21 ns | 3,177.08 ns | 0.2403 |      - |    3048 B |
| NinoDeserialize        |   275.37 ns |   273.68 ns |   277.51 ns | 0.2427 | 0.0014 |    3048 B |

```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX 2.60GHz, 1 CPU, 20 logical and 14 physical cores
.NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
IterationCount=12  LaunchCount=1  WarmupCount=5
```

`Platform` 直接调用 `Utf8.FromUtf16`，`BeforeKernel` 复刻未加前缀内核的生产包装（`StringSerializer.Serialize` 的旧行为，同样的非内联调用帧），`Runtime` 为当前 `StringSerializer.Serialize`。因此 `BeforeKernel -> Runtime` 是本轮前缀内核的净效果，不含方法封装开销。

| Method       | Length | Data              | Mean         | Ratio |
|------------- |------- |------------------ |-------------:|------:|
| Platform     | 32     | Ascii             |     8.823 ns |  1.00 |
| BeforeKernel | 32     | Ascii             |     9.103 ns |  1.03 |
| Runtime      | 32     | Ascii             |    10.723 ns |  1.18 |
| Platform     | 32     | Chinese           |    53.209 ns |  1.00 |
| BeforeKernel | 32     | Chinese           |    53.375 ns |  1.00 |
| Runtime      | 32     | Chinese           |    22.319 ns |  0.42 |
| Platform     | 32     | ChineseFirstMixed |    28.697 ns |  1.00 |
| BeforeKernel | 32     | ChineseFirstMixed |    29.825 ns |  1.04 |
| Runtime      | 32     | ChineseFirstMixed |    34.977 ns |  1.22 |
| Platform     | 32     | Emoji             |    52.212 ns |  1.00 |
| BeforeKernel | 32     | Emoji             |    53.313 ns |  1.02 |
| Runtime      | 32     | Emoji             |    55.616 ns |  1.07 |
| Platform     | 4096   | Ascii             |   200.220 ns |  1.00 |
| BeforeKernel | 4096   | Ascii             |   196.376 ns |  0.98 |
| Runtime      | 4096   | Ascii             |   198.434 ns |  0.99 |
| Platform     | 4096   | Chinese           | 6,065.075 ns |  1.00 |
| BeforeKernel | 4096   | Chinese           | 6,069.094 ns |  1.00 |
| Runtime      | 4096   | Chinese           | 1,795.497 ns |  0.30 |
| Platform     | 4096   | ChineseFirstMixed | 2,400.012 ns |  1.00 |
| BeforeKernel | 4096   | ChineseFirstMixed | 2,401.673 ns |  1.00 |
| Runtime      | 4096   | ChineseFirstMixed | 2,326.426 ns |  0.97 |
| Platform     | 4096   | Emoji             | 7,716.247 ns |  1.00 |
| BeforeKernel | 4096   | Emoji             | 7,617.156 ns |  0.99 |
| Runtime      | 4096   | Emoji             | 7,504.783 ns |  0.97 |

结论：纯中文 4096 单元约 3.4 倍、32 单元约 2.4 倍加速；`ChineseFirstMixed` 只在前 8 单元为纯 3 字节 BMP 时进入内核，首个块含 ASCII 时会放弃内核，短串约多 5 ns（约 1.2 倍）。emoji 起始的字符串被新守卫直接跳过，开销降至噪声范围。ASCII 与混合长串变化在噪声内。所有分组分配均为 0 B/op。

内核只在 `source.Length >= 32 && destination.Length >= 24 && source[0]` 为 0x0800..0xFFFF 的非代理项时尝试，且只写出精确的 24 字节/8 单元，不依赖调用方预留 padding，尾部一律交给平台转码器，保持 `OperationStatus`、已消费长度和替换/严格语义不变。

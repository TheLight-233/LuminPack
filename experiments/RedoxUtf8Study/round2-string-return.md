```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX, 20 logical and 14 physical cores
  [Host]     : .NET 10.0.9, X64 NativeAOT x86-64-v3
  Job-WBUSZU : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
IterationCount=12  LaunchCount=1  WarmupCount=6
```

`LuminPackSerializer.SerializeJson<T>()` 返回 string，分别用 UTF-8 与 UTF-16 内部输出。有效输入下两种编码返回的 string 完全相同（`GlobalSetup` 中已断言）。中文模型为约 5.2 KB 中文文档，ASCII 模型为基准项目已有的 `CharacterSaveData`。

| Method      | Mean      | Ratio | Allocated |
|------------ |----------:|------:|----------:|
| Cjk_Utf8    |  9.916 us |  1.00 |   5.21 KB |
| Cjk_Utf16   |  4.965 us |  0.50 |   5.21 KB |
| Ascii_Utf8  | 32.393 us |  3.27 |  67.44 KB |
| Ascii_Utf16 | 37.333 us |  3.77 |  67.44 KB |

结论：UTF-16 内部输出省掉“UTF-8 转码 + 再解码回 UTF-16”的两次转换，中文文档快 2.0 倍；但纯 ASCII 模型反而慢约 15%，因为输出缓冲翻倍，且写入时的转码本身已接近拷贝成本。两边分配相同，都是最终字符串。

因此**不改变默认**：`SerializeJson<T>()` 继续使用 UTF-8 内部输出。中文为主的调用方可显式传 `StringEncoding = LuminPackStringEncoding.UTF16` 获得约 2 倍加速，代价是孤立代理项不再替换为 U+FFFD 而是原样保留（这正是既有 UTF-16 输出语义），以及 ASCII 负载的 15% 回退。

不建议为此在 writer 中累计 UTF-16 长度：那需要在每个写入点计数，而收益只来自省去一次计数扫描，量级远小于此处 2 倍，也不改变“先转码再解码”的本质。

基准源码为 `JsonStringReturnBenchmark.cs`。
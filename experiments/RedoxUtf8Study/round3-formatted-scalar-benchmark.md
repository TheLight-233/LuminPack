```
BenchmarkDotNet v0.15.6, Windows 11 (10.0.26200.9457)
13th Gen Intel Core i7-13650HX, 20 logical and 14 physical cores
.NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
IterationCount=12  LaunchCount=1  WarmupCount=6
```

每行写入 1000 个值。`*_ToStringThenWriteString` 复刻改动前的 `WriteString(value.ToString(...))`，`*_DirectFormat` 是新的栈上格式化。两者走同一个写入器与同一段内容写入代码，只差文本来源，因此同一次运行内的比值可直接比较；跨运行的绝对耗时在本机波动较大（基线在三次运行间从 64 到 38 µs），所以只取配对比值。

| Method | Mean | Ratio | Allocated | Alloc Ratio |
|---|---:|---:|---:|---:|
| DateTimeOffset_ToStringThenWriteString | 62.46 us | 1.00 | 88,536 B | 1.000 |
| DateTimeOffset_DirectFormat | 44.77 us | **0.72** | 536 B | **0.006** |
| DateTime_ToStringThenWriteString | 55.98 us | 0.90 | 80,536 B | 0.910 |
| DateTime_DirectFormat | 40.99 us | **0.66** | 536 B | **0.006** |
| Guid_ToStringThenWriteString | 52.49 us | 0.84 | 96,536 B | 1.090 |
| Guid_DirectFormat | 28.13 us | **0.45** | 536 B | **0.006** |
| TimeSpan_ToStringThenWriteString | 46.60 us | 0.75 | 56,520 B | 0.638 |
| TimeSpan_DirectFormat | 28.30 us | **0.45** | 536 B | **0.006** |
| DateTimeOffset_DirectFormat_Utf16 | 31.68 us | 0.51 | 536 B | 0.006 |

结论：耗时降到 0.45–0.72 倍，分配降到 0.6%。剩下的 536 B 是基准自身每次调用构造一个 `LuminPackWriterOptionalState` 的开销，不是每个值的开销；xUnit 用例走线程静态状态池，64 个值 × 4 种类型实测 **0 B**。UTF-16 输出同样零分配，只是每字符写两个字节，故比 UTF-8 慢。

复用 byte[] 的 1000 个 DateTimeOffset 整包序列化（栈探针，非 BDN）：分配从 160,048 B 降到 72,048 B，减少 55%，与「1000 × 88 B 的 ToString 字符串」完全对应。

基准源码为 `JsonFormattedScalarBenchmark.cs`。
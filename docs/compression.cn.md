# 压缩

[English](compression.md)

`LuminPack.Utility.LuminCompressor` 用于压缩字节跨度。`[LuminPackCompress]` 对非托管数组成员使用相同的编解码器。压缩功能独立于二进制和 JSON 序列化器。

```csharp
byte[] compressed = LuminCompressor.Compress(input);
byte[] restored = LuminCompressor.Decompress(compressed);

byte[] storage = new byte[LuminCompressor.GetMaxCompressedSize(input.Length)];
int written = LuminCompressor.Compress(input, storage);
int originalLength = LuminCompressor.GetDecompressedSize(storage.AsSpan(0, written));
byte[] output = new byte[originalLength];
LuminCompressor.Decompress(storage.AsSpan(0, written), output);
```

跨度重载使用调用者提供且互不重叠的输入和输出。即使实际结果较小，压缩目标也必须满足完整的最大容量要求。解压目标必须容纳声明的输出长度，返回值是实际写入字节数。`LuminBufferWriter` 重载允许输入和输出为同一个 writer：先暂存结果，再替换原内容。

`GetMaxCompressedSize(0)` 返回 8。非空输入的容量上界为 `8 + length + (length >> 7) + 16`；负数及超过 2,130,836,464 的长度会抛出 `ArgumentOutOfRangeException`。该上界覆盖字面量扩展和序列开销，包括不可压缩数据。

C++ 通过 `<luminpack/compression.h>` 和 `luminpack::compression` 提供相同的帧格式。调用 `max_compressed_size(input.size())` 分配目标缓冲区，再按 `compress(input, destination)` 返回的字节数截取压缩帧。`decompressed_size(frame)` 给出 `decompress(frame, destination)` 所需的目标容量。输入和输出是调用方提供且互不重叠的跨度。解码损坏的帧时，目标缓冲区可能保留已解码的前缀。

## 帧格式

所有多字节定长整数采用小端序。帧以 ASCII `LUM2` 和非负 `Int32` 原始长度开头。空输入仅包含这八字节头部。解码器仅接受 `LUM2` 标识。

非空帧包含以下序列：

| 字段 | 编码 |
| --- | --- |
| Token | 高半字节：字面量长度，最大记录为 15。低半字节：匹配长度减 4，最大记录为 15。 |
| 字面量扩展 | 高半字节为 15 时存在，解码值加到 15 上。 |
| 字面量 | 恰好为解码后的字面量长度。 |
| 匹配距离 | 非零 `UInt16`，不能超过已产生的输出字节数。 |
| 匹配扩展 | 低半字节为 15 时存在，解码值加到 19 上。 |

小于 255 的扩展值占一个字节。否则先写入 `FF`，再将扩展值减 255 编码为规范的无符号 base-128 整数：每字节低七位存放数据，低位组在前，最高位表示后面还有字节。半字节饱和时必须写入扩展，包括零值。值必须能表示为非负 `Int32`；冗余的末尾零组、未终止的编码以及溢出均被拒绝。例如，扩展值 128、254、255、383 分别编码为 `80`、`FE`、`FF 00`、`FF 80 01`。

最后一个序列的匹配半字节为零，仅包含字面量及其可选扩展，不跟随匹配距离。解码器要求存在结束序列，且实际输出长度与头部声明完全相同。

## 匹配与执行

编码器采用 64 KiB 窗口、每线程复用且不清零的低 16 位位置表、跳步扫描、向后扩展和单步懒匹配。读取候选位置前会验证其模距离。每线程最多保留 32,768 个表项（64 KiB）；预热后的跨度操作不需要托管分配。

懒匹配先判断候选是否可能超过当前匹配，并比较该边界处的四字节。SIMD 扩展每组比较四个完整的 128、256 或 512 位向量，直接从已经加载的差值中定位首个不同字节。每次加载均位于已验证范围内，硬件条件选择可用宽度；标量和便携路径保留有界尾部及 32 位块复制规则。

长周期匹配通过互不重叠的复制逐步扩大已初始化前缀。每次完成复制都会增加可读前缀，因此即使原始匹配距离小于向量宽度，共享复制内核仍能使用 SIMD。短匹配保留有界的标量或向量复制。`netstandard2.1` 仍可使用原生 Burst 匹配钩子。

## 验证

解码器在访问前检查字面量输入和输出范围、匹配距离、扩展规范性、输出长度和结束帧结构。`GetDecompressedSize` 验证头部；当声明的输出超过帧体长度的 255 倍时，会在分配或扩容前检查完整序列结构。这允许紧凑的长匹配，同时避免只凭一个大长度头部就分配内存。普通帧不会在此进行完整验证，其序列由 `Decompress` 验证。跨度解码失败时，目标中可能已写入有效前缀。

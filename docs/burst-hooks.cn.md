# Core 可选原生内核

[English](burst-hooks.md)

`LuminPack.Core.LuminPackBurst` 只在 `NETSTANDARD2_1` 下编译。新版 .NET 资产既没有这张表，也没有对应的调用点检查，保留原生 SIMD 派发。Core 不依赖 Unity/Burst，也不会自动安装任何后端。可选的 `LuminPack.Burst` 扩展导入 Unity 后，会在场景加载前注册其可用的后端。

设置 `KernelTable.Version = LuminPackBurst.AbiVersion`，填入支持的 Cdecl 指针，其余槽位保持 null，然后调用 `Register`。ABI 版本 3 在托管表末尾追加了紧凑 Latin-1 和预规划布局槽位；已有的版本 2 注册仍然受支持，追加的槽位被禁用。不要把这个托管结构当作无版本的原生内存块传递。没有兼容后端的平台不注册这张表。缺失的槽位保留原有的标量/平台路径。布局转换返回 0 时，剩余工作交给原有的逐字段代码。

注册会复制并原子地发布一个不可变的表快照。之后修改调用方的表不会产生影响。每次调用都会捕获自己的快照。`Clear()` 停止后续发布；它不会等待正在进行的调用结束，也不会卸载原生代码。在捕获的调用结束之前，原生代码以及任何委托/编译器所有者都必须保持存活。卸载或替换请使用应用控制的静止点。内核不得保留指针、调用托管回调、跨 ABI 抛出异常，或递归调用自己的派发器。

预规划布局后端共享 `LuminKernelPolicy.UseNativeLayout`：少于 512 个元素的批次在创建原生描述符之前保留 Core 原有的字节段或生成的逐字段路径。这只决定原生边界的适用条件；新版 .NET 的直接向量路径保留其原有的完整访问余量。`LuminLayout` 转换器和 `LuminPortable` 打包/解包入口都缓存各自方向的规划。可移植工厂保留原有的四段宽元素适用条件和保守的完整向量余量；通用转换器保留自己原有的三段适用条件。原有的基于偏移的槽位对版本 2 注册仍然可用。

所有槽位都使用 `delegate* unmanaged[Cdecl]`。`int`、`uint` 是有符号/无符号 32 位整数，`char` 是 UTF-16 码元，`byte` 是 8 位。布尔结果为 0/1。参数顺序如下：

| 槽位 | 参数与结果 |
| --- | --- |
| `Copy` | `byte* destination, byte* source, uint bytes -> int`。ABI 版本 2 完成时返回 1，或者不写任何内容返回 0，交回 Core 原有的 `Unsafe.CopyBlockUnaligned` 回退。Core 只在 64 位上派发 16..256 字节；32 位固定存储和更大的范围保留 cpblk。`LuminKernelPolicy` 共享原有的 128/256/512 位短复制适用条件。 |
| `Equal` | `byte* left, byte* right, uint bytes -> int`。普通的完整字节相等比较；绝不用于认证标签。 |
| `Utf8Encode` | `char* source, int characters, byte* destination, int capacity, int* consumed, int* written, uint flags -> int`。标志 1 允许替换非法输入；标志 2 表示最后一块输入。计数分别为输入字符数/输出字节数。 |
| `Utf8Decode` | `byte* source, int bytes, char* destination, int capacity, int* consumed, int* written, uint flags -> int`。标志相同；计数分别为输入字节数/输出字符数。 |
| `Utf8Equal` | `byte* source, int bytes, char* candidate, int characters -> int`。只有合法的规范 UTF-8 恰好解码为 candidate 时为真。拒绝超长编码、代理标量、不完整序列和超范围标量。 |
| `AsciiEncode` | `char* source, int characters, byte* destination, uint flags -> int`。容量至少为 `characters`。写入前校验全部输入。拒绝非 ASCII；标志 1 还拒绝 0x20 以下的控制字符、引号和反斜杠。返回假时不写任何内容。不添加引号或终止符。 |
| `Layout` | `byte* source, byte* destination, int count, int sourceStride, int destinationStride, int* sourceOffsets, int* destinationOffsets, int semanticBytes -> int`。偏移对按声明顺序描述语义字节。返回 `[0,count]` 内的完整元素前缀；之后的元素保持不动。后面重叠的字段优先。不访问完整源/目标元素之外的任何字节。 |
| `CountMatch` | `byte* left, byte* right, uint maximum -> uint`。返回 `[0,maximum]` 内的完全相等前缀长度。输入可以重叠且保持只读。 |
| `CopyMatch` | `byte* destination, uint bytes, uint backwardDistance -> void`。正的向后引用和完整输出由解析器校验。按逻辑字节递增顺序展开：短周期需要反复重叠的语义，不能用 memcpy/memmove。 |
| `ChaCha20` | `uint* state, byte* output, int blocks -> void`。16 个状态字保持不变。blocks 为 1/2/4，输出有 `64 * blocks` 字节。连续的块以原有的 32 位回绕语义递增第 12 个字；保持 RFC 字节序。 |
| `Xor` | `byte* output, byte* input, byte* keystream, uint bytes -> void`。完整范围；支持输入与输出完全别名。 |
| `IsLatin1` | `char* source, int characters -> int`。检查整个范围内码元是否都不超过 U+00FF，不写内容。 |
| `NarrowLatin1` | `char* source, byte* destination, int characters -> int`。输出能容纳每个字符。返回 1 表示所有码元都已窄化。返回 0 表示存在无法表示的码元，可能已写出一段窄化前缀，与 Core 现有契约一致；记录写入器会用原始 UTF-16 覆盖该前缀。 |
| `WidenLatin1` | `byte* source, char* destination, int characters -> void`。完整输入和未发布的输出范围；逐字节精确展宽。 |
| `Latin1Equal` | `byte* source, char* candidate, int characters -> int`。长度相同的完整范围；把负载字节与 UTF-16 码元逐一比较，不按 UTF-8 解释。 |
| `LayoutPlanned` | `byte* source, byte* destination, int count, LuminLayoutPlan* plan -> int`。固定的、进程内不可变的描述符包含 Core 预先计算的步长、掩码和完整访问余量。返回 `[0,count]` 内的完整前缀。Core 用原有的字节段处理尾部。版本 2 注册禁用这个追加的槽位。 |

UTF 编解码返回 `OperationStatus` 整数：Done 0、DestinationTooSmall 1、NeedMoreData 2、InvalidData 3。只有在尚未消费或写入任何内容、两个计数都为 0 时才可以返回 -1，交回原有的编解码器。流式处理、非法序列的消费、替换和输出容量的语义必须与平台编解码器一致。Done 表示消费了全部源数据。Core 在发布托管游标前校验计数和状态；这种校验无法防止错误的原生内存访问。

托管的输入、输出和布局描述在整个原生调用期间都被固定。ChaCha/XOR 收到的已经是调用方固定的、原生的或栈上的存储。空窗口可能是 null 指针，绝不能解引用。不要假设对齐。Core 保留帧、范围检查、分配/复用、引用状态、构造/回调和发布。布局描述符只在存在布局槽位时才延迟初始化；非法的几何参数会交回原有代码。

边界覆盖原始复制、UTF-16 复制/比较、UTF-8 编码/解码/复用、紧凑 Latin-1 记录、短 ASCII JSON、tagged/packed 结构体集合、LZ 匹配比较/展开以及加密/XOR。新版 .NET 的 BMP/512 位文本和 F14 向量辅助保持为原生 .NET 路径私有。旧的 F14 表示没有新的标签组算法。

未来的后端需要自己检测精确的 CPU 特性：非空指针不代表特性检查。在每个运行时上审查标量、128/256/512 位和 ARM64 行为、有界尾部、非对齐访问、真实重叠和生命周期。固定宽度的算法需要以块为单位的理由。编译和标量 ABI 探针不能验证 Burst 代码生成、IL2CPP 或本地没有的硬件。启用后端之前请遵循 [SIMD 规则](mechanism-contract.cn.md#4-simd)。

`tests/LuminPack.Tests` 的 `Hooks` 测试区（`-p:LuminPackTestArea=Hooks`）在新版宿主中用标量非托管 ABI 探针执行实际的 `netstandard2.1` 资产；它既不是发布的后端，也不是性能测试。新版运行时的单元测试另外检查 Hook 类型不存在。

# Unity 编解码审计：分配事件与标量批处理

基线为 `ea342a102281384cceee0f61163eff77abdb7dfd`。本轮覆盖 3 种原生所有者和可选模块全部 21 种显式值编解码器。这完成的是 Unity 类型组的首次独立审查，不代表全库性能目标完成，也不代表全路径零额外 GC。

## 修改与保留的契约

15 种仅含标量的值读取先校验完整记录，再按常量字节偏移取得显式字段，最后执行一次带检查的 Advance 并发布本地结果。嵌套调用间接受益，未展开其原有契约。每次类型化访问仅针对不超过 4 字节的 float、int、byte 或 WeightedMode；不假定托管字段相邻，也不读取 Unity 私有布局。

8 字段 Keyframe、16 字段 Matrix4x4 写入预留完整记录后只推进一次。小型写入保持原实现：试验中的 Vector3 批量写入约慢 3%，机器码也更大，因此已撤回。

曲线尺寸计算直接读取公开 length；写入在任何输出回调前取得完整键快照，新版使用 Span API，旧版使用公开索引器。Unity 6000.2 起的曲线读取和渐变尺寸／读写使用公开 Span API。字段顺序、完整预校验、复用对象、原生 setter、元数据和游标发布保持不变。

每组最多 16 个键用有界栈暂存，更大时租借并归还共享数组。读取在取得暂存前拒绝无效计数和负载。现代 getter 前清空捕获缓冲区，避免不受支持的并发缩短暴露未初始化数据；原有“尺寸计算和写入期间不得并发修改所有者”的要求不变。

原生所有者 JSON 仍明确拒绝。维护测试覆盖 UTF-8／UTF-16、null／非 null 根值、null／对象输入以及拒绝时目标身份不变。

## 验证

实际授权 Windows Editor 2022.3.48f1c1 和 6000.3.2f1 均执行了定向原生所有者契约、生成的字段／数组／列表／复用、36 次 JSON 拒绝调用及全部 21 个值编解码器。值测试包含每个截断前缀、精确输出窗口、边界哨兵、非零读取位置及输入与目标重叠。最终日志 editor-2022-complete.log、editor-6000-complete.log 均有 LUMINPACK_UNITY_AUDIT_OK。

维护的值测试程序还通过 net10 x64，以及使用相同 netstandard 模块和 Core 的 net8 x86 检查。独立生成消费者构建通过，仅保留 NativePayload.cs 原有的一处可空警告。仓库规范检查覆盖 729 个 C# 文件、6226 个声明，含新增的现代 Unity 条件分支，违规数为 0。PowerShell 语法和本机版本识别通过。仅扩展 CI 矩阵新增 6000.3.2f1，对应 unityci 镜像标签已通过 Docker Hub API 确认；本机没有执行 CI。

未运行全量测试或全量 Benchmark。本次改动的 ARM、IL2CPP、WebGL 和 Player 执行仍未验证。

## 分配证据与限制

Unity Mono 的 GC.GetAllocatedBytesForCurrentThread 连必要的结果构造都返回 0，早期字节结果无效，不作为证据。最终使用限定当前线程、不按帧聚合的 GC.Alloc ProfilerRecorder，检查容量溢出并包含明确分配的阳性对照。**CSV 数值是每次操作的托管分配事件数，不是字节数。** GC.Alloc 的样本值是时间数据，不能求和当作字节。各操作独立预热；维护测试仅对不依赖池保留的栈路径要求严格零暂存分配。

最终现代路径中，曲线／渐变的尺寸、写入、复用读取记录为 0；新建读取为 1，与直接创建所有者一致。基线曲线写入／复用读取有 1 个临时数组，渐变有 2 个。RectOffset 保持写入／尺寸／复用 0、新建 1；全部值读写为 0。旧版曲线写入和尺寸为 0，但读取仍有 1 个键数组；旧版渐变仍有 2 个键数组，新建读取另需结果包装对象。

中间一次现代运行在 128 次、17 键曲线新建读取中记录到额外一次池分配；旧版也在大量分配的读取后出现过池分配。这是实际池未命中／回收，不能删除或当噪声忽略。池路径不保证无条件零额外 GC，测试保留日志但不假设池能跨 GC 永久保留。最终 size 行的第三列是直接构造结果的分配对照，不是参考库尺寸操作；尺寸不计时。

## 同轮性能

机器为 Intel Core i7-13650HX、Windows x64；运行实际 Editor Mono，并显式使用 -releaseCodeOptimization -disableManagedDebugger。基线、候选和 MemoryPack 在同一进程、同一输入、复用输出缓冲区下交替执行，没有与构建或其他实验重叠。每个计时操作先预热 512 次，采集 24 个样本，每样本 4096 次，轮换实现顺序；分配窗口每操作独立预热 64 次后测量 128 次。原生新建读取只测分配、不计时。保留全部样本与离群点。

参考为 MemoryPack.Core 1.21.4 和同标签的官方 Unity formatter。Gradient 适配器额外保留 colorSpace，使对象字段对等；计时前检查原生对象值和 LuminPack 前后字节一致。值类型参考使用 MemoryPack unmanaged 路径，其布局／线格式政策与 LuminPack 显式字段契约不同。参考源码仅为本地带 MIT 许可的稀疏检出，没有复制进产品。

下表为 LuminPack 耗时／MemoryPack 耗时的中位数，越低越好。这些是编解码器局部观测，不是全库加速比例。

| 引擎 | 操作 | 优化前／参考 | 优化后／参考 |
| --- | --- | ---: | ---: |
| 6.3 | 16 键曲线写入 | 2.556 | 1.459 |
| 6.3 | 16 键曲线复用读取 | 3.449 | 1.949 |
| 6.3 | 257 键曲线写入 | 5.031 | 2.775 |
| 6.3 | 257 键曲线复用读取 | 6.635 | 4.015 |
| 6.3 | 4 键渐变写入 | 1.032 | 0.514 |
| 6.3 | 4 键渐变复用读取 | 2.243 | 1.565 |
| 2022.3 | 16 键曲线写入 | 1.925 | 1.213 |
| 2022.3 | 16 键曲线复用读取 | 3.497 | 2.725 |
| 6.3 | Vector3 读取 | 4.445 | 3.518 |
| 6.3 | Matrix4x4 读取 | 6.601 | 4.271 |
| 6.3 | Keyframe 读取 | 8.762 | 6.547 |

离散程度不可忽略。现代 257 键写入的优化前／后／参考 CV 为 1.55%／1.25%／4.72%；16 键读取为 6.54%／2.32%／20.20%。一些小输入或参考行超过 30%。两个 summary.csv 保留全部中位数和 CV，timing.csv 各保留 1488 行原始样本。Matrix 写入两版引擎略有改善，但机器码大 22 字节，不声称稳定的大幅加速；LayerMask 读取基本持平。未修改的旧 Gradient 写入及 RectOffset 对照也有波动。调试优化模式和被撤回候选的数据保留在本地，不作为最终性能证据。

## IL 与机器码

最终 il.csv、native-summary.csv 同时包含前后实现。原生字节从实际预热过的 Mono JIT 方法取得，使用公开 jit-info API，并以 Capstone 5.0.6 解码，包含冷路径。现代读取体积分别从 Vector3 的 2213→928、Matrix4x4 的 11110→2256、Keyframe 的 5782→1624 字节；IL 局部变量从 4／17／9 降至 2／2／2。读取边界检查和最终带检查推进均保留。Keyframe 写入 2312→2270 字节，Matrix4x4 写入 1840→1862；小型写入已恢复，Vector3 均为 752 字节。

汇编还暴露出 Mono 将 Core Position 的字节指针相减翻译为除以 1 的整数除法。这是下一轮 Runtime 实验的明确线索，影响面超过可选模块。本轮未修改它，仍需独立参考比值和带检查位置语义验证。

## 复现与后续

probe/UnityOwnerAudit.cs 是输入、计时与分配夹具，probe/NativeDump.cs 捕获原生方法。基线为指定提交 Runtime 下四个编解码源码，仅改命名空间为 LuminPack.UnityBaseline。与当前 Runtime、官方 MemoryPack UnityFormatters.cs 一起构建 netstandard2.1 探针，引用所选引擎；只有 6000.2+ 定义现代版本符号。先构建 Core／Generator，再用生成器编译维护的 Consumer。隔离 Editor 工程加载探针、Core、Unsafe、Immutable、MemoryPack.Core，依次运行 UnityObjectContract、维护值测试、ContractEntry 和 UnityOwnerAudit。低层托管输入必须在原生调用和分配期间持续固定。原始本地 prepare.py 仅用于初始搭建，不得覆盖已保存的基线或最终探针工程。

本报告保留最终原始 CSV 和夹具。引擎库、Library、参考源码检出、早期日志和原生二进制留在本地。类型表现在可以标为全部已有审查或部分覆盖记录，但 portable BitArray 暂存、Type／CultureInfo 的 BCL 读取、旧 Unity 数组、冷池、此前包装器／复用缺口以及全库 Runtime／生成器／JIT／SIMD 审计仍未完成。

API 依据：[Unity 曲线](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.2.0f1/Runtime/Export/Animation/AnimationCurve.bindings.cs)、[Unity 渐变](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.2.0f1/Runtime/Export/Math/Gradient.bindings.cs)、[线程分配记录](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)、[Mono JIT 元数据](https://raw.githubusercontent.com/Unity-Technologies/mono/unity-main/mono/metadata/appdomain.h)。这些用于核对 API，没有复制其产品实现。

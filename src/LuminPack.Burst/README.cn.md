<div align="center">

# 🔥 LuminPack.Burst

**为 Unity 中的 LuminPack 提供 Burst 编译的 SIMD 内核**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![Burst 1.8](https://img.shields.io/badge/Burst-1.8-000000?style=flat-square&logo=unity&logoColor=white) ![ABI v3](https://img.shields.io/badge/kernel%20ABI-v3-F97316?style=flat-square) ![x64 | ARM64](https://img.shields.io/badge/arch-x64%20%7C%20ARM64-2EA043?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [Unity 指南](../../docs/unity.cn.md) · [Burst 钩子](../../docs/burst-hooks.cn.md) · [Unity 值编解码器](../LuminPack.Unity/README.cn.md)

[快速开始](#-快速开始) · [内核](#-内核) · [硬件](#-硬件) · [生命周期](#-生命周期) · [安装与构建](#-安装与构建) · [测试](#-测试)

</div>

---

可选的 Burst 1.8 后端，实现 netstandard2.1 `LuminPack.Core.LuminPackBurst` ABI 的第 3 版。它引用 Core、`Unity.Burst` 和 `UnityEngine.CoreModule`；Core 与 LuminPack.Unity 都不引用它。现代 .NET 构建继续使用自己的原生 SIMD。

## 🚀 快速开始

```csharp
using LuminPack.Burst;

bool installed = BurstBackend.TryRegister();
```

- `Register()` 带有 `BeforeSceneLoad` 运行时钩子，其声明类带有 IL2CPP 预先构造特性。
- 仍然可以手动调用 `TryRegister()`；若已存在 Core 表，它返回 `false` 且不会替换。
- 禁用 Burst、WebGL 以及探针报告没有编译出 SIMD 的情况，都会保留 Core 的路径。
- 编译完成后才会发布表，因此失败绝不会发布不完整的条目。

## 🧮 内核

该表会填满全部 **16** 个 ABI 槽位：

| 领域 | 槽位 |
| --- | --- |
| 内存 | 复制 · 普通相等比较 · 相等前缀匹配 · 周期性匹配扩展 · XOR |
| UTF-8 | 编码 · 解码 · 相等比较 · 短 ASCII 编码 |
| 紧凑 Latin1 | 分类 · 窄化 · 宽化 · 相等比较 |
| 布局 | 前缀转换 · 预计算的布局前缀 |
| 密码 | ChaCha20 块生成 |

### 从 Core 投影而来，绝不分叉

这些内核由 **现有 Core 方法的自动投影** 编译而来。`CodeGen~` 使用 Roslyn 读取权威的 Core 语法，`Runtime/Generated` 在 .NET 构建时重新生成并带有源码哈希。原有的分支、遍历顺序、完整块边界和尾部处理都来自 Core。`KernelVectors`、`KernelMemory` 与 `PortableUtf8` 负责映射原生原语以及 Core 的可移植平台回退。

> [!CAUTION]
> 绝不要手动修改生成的算法。把源码导出到 Unity 之前先运行投影工具，并使用 `--verify` 检测过期的输出。`--core-source-root` 可以指定一份固定的源码副本，用于无需构建 Core 的可复现隔离验证。

<details>
<summary><b>内核细节</b></summary>

**复制。** `LuminKernelPolicy` 由 Core 与 Burst 共享。复制在 256 字节以内使用原有的头/中/尾调度；只有窄通道可用时上限为 128 字节。必须是 64 位进程并具备所需硬件。不支持的大小或特性会直接返回 0 且不写入，由 Core 执行其现有的 `Unsafe.CopyBlockUnaligned` 回退。不存在第二套大块复制循环，也没有替代的流式策略。

**匹配与 XOR** 保留 Core 精确的向量阈值和标量尾部。周期性扩展保留 Core 的填充/不重叠/重叠分支以及 8 字节余数路径。

**文本。** UTF 编解码器保留替换行为、流式状态以及消费/产出计数。ASCII 保留 Core 的适用条件，并在写入之前先校验。由于 Burst 1.8 拒绝 `char*` 外部函数，原生文本入口把 UTF-16 码元表示为 `ushort*`；表的发布使用 Core 未改动且二进制等价的指针签名。普通相等比较映射到平台的内存比较原语，而认证标签比较仍走 Core 原有的常数时间路径。

**紧凑 Latin1。** 默认 UTF-16 配置下的紧凑 Latin1 记录使用专门的分类、窄化、宽化和复用比较槽位。它们保留完整的码元以及现有的紧凑/原始记录头。缺失的槽位保留 Core 的可移植路径。UTF-8 槽位只服务于自己的编码；请根据应用的数据契约选择编码，并在比较的两端使用相同的配置。

**布局。** 布局转换共享 Core 不可变的计划构建和完整向量前缀。Core 只为每个转换器计算一次掩码，为原生调用固定该描述符，并完成其原有的字节串尾部。原有基于偏移的槽位依然可用。计划好的原生批处理使用 Core 共享的 512 元素下限；更小的范围保留 Core 的逐字段路径。

**密码与查找表。** 密码块使用 Core 原有的轮函数，并在状态的私有副本上分派。混合 Latin1 UTF-8 洗牌表在投影时由 Core 原有的表构建器求值，并以原始常量输出——绝不单独维护。Burst 无法调用平台 UTF-8 API 时，使用 Core 的可移植编解码器和现有的 ASCII 宽化内核。

</details>

## 💻 硬件

| 平台 | 路径 |
| --- | --- |
| 带 AVX2 / SSE2 的 x64 | 最多 256 位块（Burst 1.8 暴露的最大宽度） |
| 带 AVX-512 的 x64 | 使用受支持的 256/128 位块和精确尾部——不会臆造不受支持的 512 位操作 |
| ARM64 | Neon |
| ARM32、x86、已禁用的后端 | Core 的路径 |

托管 Burst 内在函数的特性属性会报告 `false`，因此需要一个编译后的能力探针，防止把托管标量回调注册成原生 SIMD。探针只是特性证据——不是正确性或速度的测量。

## 🔄 生命周期

Core 会为每次原生调用固定托管所有者，静态委托根与本后端程序集共存亡。调用 Core 的 `Clear()` 会停止发布新表，但不会等待已经捕获该表的调用。

> [!WARNING]
> 只有应用的所有者可以在受控的生命周期边界替换或清除该表。在进行中的调用完成之前，必须让本程序集及其原生代码保持存活。后端有意不提供可能抹掉其他所有者表的无条件全局注销。

## 📥 安装与构建

把 `Runtime` 及其 asmdef 导入一个包含 Core 的 netstandard DLL、其依赖以及 `com.unity.burst` 1.8.x 的 Unity 项目。

- asmdef 由包版本宏启用；没有 Burst 时，这个可选程序集会被排除。
- 导入源码 **或** 预编译扩展，不要同时导入两者。
- 源码使用 C# 9 不安全函数指针和 Cdecl 签名。
- Unity 引擎 DLL 与 Burst 二进制由你的项目提供，不随本仓库分发。

不调用 Core 或其生成器的隔离构建：

```powershell
dotnet build src/LuminPack.Burst/LuminPack.Burst.csproj -c Release `
  -p:UnityManagedPath="C:/Unity/Editor/Data/Managed/UnityEngine" `
  -p:UnityBurstAssemblyPath="C:/UnityProject/Library/ScriptAssemblies/Unity.Burst.dll" `
  -p:LuminPackCorePath="C:/Artifacts/LuminPack.Core.dll"
```

## 🧪 测试

`Tests~` 包含可移植的差分检查，以及一个使用真实 Burst 函数指针的编辑器测试夹具。契约覆盖完整范围、对齐、每一个不匹配位置、复制拒绝与原生回退、别名、周期重叠、编解码器标志/状态/计数、格式错误的文本、JSON 转义、布局声明顺序，以及带计数器回绕的密码 RFC 向量。

要运行原生检查，请把 `KernelContract.cs`、`Shared`、`Editor` 和测试 asmdef 复制到 Unity 项目的测试文件夹中（不要复制控制台 `Program`）。在 x64 SSE2/AVX2 与 ARM64 上执行该夹具，并在发布前测试 Mono 和 IL2CPP 播放器。

手动触发的 **Unity extensions** 工作流使用预先授权许可的 Windows x64 与 macOS ARM64 运行器，以及 `.github/scripts/invoke-unity-extensions.ps1`。它只在一个隔离的临时项目中运行扩展契约。

> [!IMPORTANT]
> 可移植执行不能证明硬件覆盖，被跳过的测试或成功的 C# 构建也不是原生验证。ARM64 与 IL2CPP 的发布验证仍需要在这些目标上真实运行——配置好工作流并不等于有执行证据。Core 现有的硬件策略要求依然有效。

---

这些内核实现的是 LuminPack 独立定义的 ABI。API 约定遵循 [Unity 官方的函数指针文档](https://docs.unity.cn/Packages/com.unity.burst%401.8/manual/csharp-function-pointers.html)。

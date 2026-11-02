# Fuzz 测试

[English](fuzzing.md)

Fuzz（模糊测试）会自动探索普通示例容易遗漏的输入：截断记录、伪造长度、翻转比特、损坏字符串、破坏 JSON 语法。LuminPack 应当一致地解码出值，或者以已知的解析错误拒绝输入。崩溃、卡死、资源耗尽，以及成功读取后的结果不一致，都是需要调查的问题。

`tests/LuminPack.Fuzzing` 是单元测试的补充。确定性冒烟测试重放种子及其变异；[SharpFuzz](https://github.com/Metalnem/sharpfuzz) 对编译后的代码插桩，让 [libFuzzer](https://llvm.org/docs/LibFuzzer.html) 根据覆盖率引导变异，探索新的执行路径。一次有限运行通过，只说明这些输入通过了检查，并不能证明所有模型和平台都安全。

## 快速开始

准备 PowerShell 7、.NET 10 运行时，以及仓库要求的 .NET 11 预览版 SDK。在仓库根目录运行：

```powershell
# 发布隔离构建、验证有效种子、重放每个种子的全部截断前缀，
# 再对每个目标执行 1,000 次确定性变异。
pwsh -File .github/scripts/fuzz.ps1

# 调整变异次数、随机种子，或测试 Debug。
pwsh -File .github/scripts/fuzz.ps1 -Configuration Debug -Iterations 2000 -Seed 42
```

冒烟运行器会在执行前，把当前输入保存到 `.tmp/fuzz/repro`，同时记录目标、随机种子、案例及重放命令。即使进程发生致命故障，最后一个输入也仍然保留。成功输出分别统计接受与拒绝数量；拒绝畸形输入属于正常结果。

## 目标与限制

| 目标 | 输入 |
| --- | --- |
| `binary` | 普通二进制对象，采用默认 UTF-16 定长字符串头，包括紧凑 Latin-1 表示。 |
| `binary-utf8-varint` | 相同对象图，采用 UTF-8 字符串和规范 base-128 长度头。 |
| `json-utf8` | 无 BOM 的严格 UTF-8 JSON 字节。 |
| `json-utf16` | 无 BOM 的严格小端 UTF-16 JSON 字节，也会测试畸形的奇数字节尾部。 |

模型覆盖整数及可空标量、布尔字段批次、字符串、基础类型及布尔数组、布尔列表、嵌套对象列表、字典和递归子对象。二进制声明启用 `UntrustedData`：输入最多 16 KiB，每个集合最多 128 个元素，每个字符串最多 512 个 UTF-16 代码单元，递归深度最多 16。JSON 声明启用严格语法和文档深度 16。每种编码均使用由声明确定的配置及具体类型的生成重载。与库的现有策略一致，JSON 集合和字符串由输入大小限制约束。

首批任意字节目标覆盖普通对象契约，尚不覆盖 Union、版本容错、引用跟踪、Custom、压缩、加密及其他字符串配置。这些机制需要具备适当契约的独立目标，不能直接启用不受支持的不可信类型形状。

开始变异前，有效种子必须还原出其来源对象图。读取成功后，运行器检查消费长度边界、全新读取与复用读取的值一致性，以及规范化往返后序列化结果的稳定性。字典按键值比较，不要求迭代顺序一致。二进制空根保留已有目标，JSON 空根则清空目标。规范化 JSON 可能补入省略的成员而超过输入上限；这种情况仍检查复用一致性，但跳过受输入上限约束的往返读取。

仅首次解析过滤预期输入错误：`InvalidDataException`、`FormatException`、`DecoderFallbackException`、`EndOfStreamException`、LuminPack 的私有解析异常、字典重复键产生的精确 `ArgumentException` 类型，以及特定 JSON 令牌不匹配消息对应的 `InvalidOperationException`。参数/边界派生异常、任意不变量错误、不支持的操作和资源故障都会继续向外报告。复用、序列化及规范化读取阶段不做异常过滤。

## 覆盖率引导运行

从 [libfuzzer-dotnet 发布页](https://github.com/Metalnem/libfuzzer-dotnet/releases/tag/v2025.05.02.0904) 下载对应平台、固定版本 `v2025.05.02.0904` 的驱动。覆盖率模式支持该驱动的 Linux 和 Windows 平台。例如，把 Windows 驱动放到 `.tmp/libfuzzer-dotnet-windows.exe` 后运行：

```powershell
pwsh -File .github/scripts/fuzz.ps1 -Mode Coverage -Target binary `
    -LibFuzzer .tmp/libfuzzer-dotnet-windows.exe -Seconds 300 -Output .tmp/fuzz-coverage
```

Linux 下先给驱动添加可执行权限，再传入其路径。脚本会在输出目录安装 `SharpFuzz.CommandLine` 2.3.0，项目使用 SharpFuzz 2.3.0。每次运行发布一份全新副本，对 `LuminPack.Core.dll` 及使用方程序集内包含生成格式化器的 `LuminPack.` 命名空间插桩，不修改正常构建产物。仅插桩 Core 会遗漏生成读取器分支。运行器位于独立命名空间，避免 SharpFuzz 初始化之前执行插桩代码。

原生桥接程序通过程序集路径启动 `dotnet`；脚本用限定作用域的 `LUMINPACK_FUZZ_TARGET` 环境变量选择目标，并在结束后恢复原值。输入最大 16 KiB，每个输入超时十秒，驱动 RSS 预算为 1 GiB。致命故障和超时仍交给原生驱动报告；这些限制是运行保障，不是托管分配量保证。

可变、按目标隔离的语料放在 `<Output>/corpus`，或显式指定的 `-Corpus` 目录。复用该目录即可保留探索得到的输入。原始失败样本放在 `<Output>/failures/<target>`。独立测试活动应选择独立输出根目录。检查原生日志中的覆盖率特征数（`ft`）增长；脚本退出成功本身不能证明触达了有效分支。

## 重放与回归样本

```powershell
# 失败文件是原目标的原始输入，重放不需要原生驱动。
dotnet run --project tests/LuminPack.Fuzzing -c Release -- `
    --replay binary .tmp/fuzz-coverage/failures/binary/crash-EXAMPLE

# 重放某个目标语料目录中的所有文件。
dotnet run --project tests/LuminPack.Fuzzing -c Release -- `
    --replay json-utf8 tests/LuminPack.Fuzzing/Corpus/json-utf8

# 在临时目录重新生成完整的确定性种子语料。
dotnet run --project tests/LuminPack.Fuzzing -c Release -- --seed .tmp/fuzz-seeds
```

重放会报告输入被接受且不变量通过，或输入被正常拒绝。非预期故障以非零退出码结束。保留目标名称和准确字节；在插桩测试活动中用 libFuzzer 的 `-minimize_crash=1` 缩减样本，将最小样本以唯一、含义明确的文件名放入 `Corpus/<target>`，修复根因时再补上针对性的回归断言。不能通过放宽异常过滤来隐藏问题。重新生成内置命名种子时，脚本保留其他语料文件。

## 持续集成

`.github/workflows/fuzz.yml` 在推送到 `main`、`refactor`，拉取请求，每周定时任务，以及手动触发时运行：

- 推送、PR 和手动冒烟任务覆盖 Linux Debug/Release、Linux 标量回退、Windows Release 和 macOS Release。每个目标重放全部种子截断前缀，并执行固定随机种子的 1,000 次变异。
- 覆盖率任务在 Linux 上逐目标运行：推送/PR 每个目标 30 秒，每周 300 秒，手动运行采用 `seconds` 输入。各任务对全新发布副本插桩，并核验原生桥接程序固定的 SHA-256。按目标隔离的缓存会把探索得到的语料带到后续运行中，具体受 GitHub 缓存作用域及清理规则约束。
- 即使任务失败，也上传语料及保存的重现输入/失败样本。具体崩溃信息保留在任务日志中；下载对应目标的产物后即可本地重放。

任务总超时包含环境准备和配置的测试时长；很大的手动预算可能需要增大工作流超时。这些任务不代表已经验证 Unity/IL2CPP、x86 或每一种 ISA。

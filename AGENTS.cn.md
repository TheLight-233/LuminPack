# Agent 指南

[English](AGENTS.md)

LuminPack 是面向 .NET 和 Unity、基于源代码生成的二进制与 JSON 序列化库。修改代码前请先阅读：

- [机制契约](docs/mechanism-contract.cn.md)：硬性约束。对运行时代码、生成器、生成的代码、可选模块和条件编译目标都有约束力。
- [架构](docs/architecture.cn.md)：各部分在哪里、如何组合。
- [贡献指南](CONTRIBUTING.cn.md)：构建、检查和规范。

## 硬性规则

1. **保留所有既有机制。** Local + As 存储视图、Union 虚方法派发及其泛型缓存槽位、版本容忍的名称表、引用跟踪、非托管原始路径与 `StructLayout`、构造/池化/复用行为、回调、专用格式化器、基于方法指针的注册、编译期选项以及状态归属。更快的设计需要先在 `experiments/` 中做隔离实验，给出同一次运行中的参照比值、分配量和 IL/原生代码证据，并由所有者明确批准这个具体方案。性能目标本身不等于批准。
2. **Release 下保留 Reader 检查。** `EnsureReadable` 及其调用者（`Advance`、固定存储批次、Union 帧）在 Debug 和 Release 下都运行。生成的游标运算在移动游标前保留检查。集合、字符串和批量负载的检查保留。固定 Writer 存储和仅基于地址的原始内核保持各自独立的不检查契约。
3. **32 位固定值访问。** 32 位上只有不超过 4 字节的单个非托管值可以使用类型化非对齐访问；更大的值和字段批次使用 `LuminPackMarshal.CopyBlockUnaligned` 和紧凑视图（IL2CPP）。相邻字段批量复制需要真实的字段地址证明、排除 volatile 字段并保留逐字段回退。只有 `LuminPackMarshal.CopyBlockUnaligned` 能调用 `Unsafe.CopyBlockUnaligned`。
4. **SIMD。** 审查标量、128/256/512 位和 ARM64 路径；按精确的指令集特性保护；保留有界尾部和可移植回退；绝不访问已校验范围之外的内存。本地没有的硬件需要在 CI 上执行。
5. **性能证据**是 LuminPack 耗时除以参照库耗时，要求同一机器、数据、运行时、选项和同一次运行，并给出多次采样、离散度和分配量。单个孤立数字说明不了问题；这台机器噪声很大。
6. **语言。** 代码、注释、XML、诊断、测试和维护文档用英文。README 和 `*.cn.md` 中文版是例外。
7. **文档。** README 是封面页；功能文档放在 `docs`。每篇文档都有英文 `name.md` 和中文 `name.cn.md`，两个版本一起更新。README 和 `docs` 中不放发布说明、版本历史或实验报告。
8. **不要**仅凭调用点搜索就删除存储占位或外部可用、生成代码使用的成员。
9. 除非被要求，**不要提交**。
10. **提交标题。** 使用 `type(scope): description`，类型和范围用小写，描述用简洁的英文祈使句。新功能用 `feat`，修复用 `fix`，性能优化用 `perf`，结构调整用 `refactor`，测试用 `test`，文档用 `docs`，工作流用 `ci`，构建工具用 `build`，维护用 `chore`。范围选择受影响的组件，例如 `core`、`generator` 或 `benchmarks`；性能前缀是 `perf`，不能写成 `pref`。独立改动分别提交。详见[贡献指南](CONTRIBUTING.cn.md#提交规范)。

## 工作方式

- 优先运行针对性的测试（单个框架、`--filter`、某个测试区或某个 `--verify-<area>` 选择器），完整测试套件很慢。
- 如实报告结果：运行了什么、哪些失败、哪些没有验证。
- 公共用法改变时，同步更新文档（两个版本）和 `skills/luminpack`。

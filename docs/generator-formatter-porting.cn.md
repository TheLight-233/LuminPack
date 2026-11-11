# 生成器 Formatter 移植

[English](generator-formatter-porting.md)

修改操作体前，先阅读[机制契约](mechanism-contract.cn.md)。

## 职责

注册入口为 Rendering/Formatters/FormatterRegistry.cs。每个全限定的开放集合定义对应一个 SequenceFormatter。独立框架类型在 Standalone 中注册。

SharedFormatterSet 收集闭合 owner 标识和元素依赖，排序、去重后参与增量比较。owner 数量不受固定比特数限制。

SharedFormatterPublisher 调用已注册的渲染器。尚未实现的元素变体不注册，在模型投影时保持未映射状态。

BinaryEmitter 负责记录 framing 和游标衔接。集合算法由注册的成员写入器负责。渲染模型携带描述符，二进制和 JSON emitter 使用统一的集合类别。

注册表和委托在源生成器内部执行。生成到应用中的代码仍使用具体静态调用、原有 Root 和编译选项。

## 新增集合

1. 创建 Rendering/Formatters/<Family>/。按旧管线支持范围实现标量、字符串、LuminPackable 和转发模板，不同变体保留独立文件。
2. owner 渲染器使用 Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) 签名。通过闭合事实和 GeneratedNames 计算名称。
3. 新增一个 SequenceFormatter 注册项，键为命名空间和开放类型名称。填写分析 Kind、标量渲染器和已支持的可选渲染器。
4. 如果记录内联该集合，将成员写入器放在相同目录，注册为 writeMember。只需要出口标签计数器的实现可用 FormatterRegistry.Adapt 接入。
5. 按旧行为填写 profileElements、alwaysLoops、payloadLoops 和 usesCandidateCache，保持 profile 签名、内联判断及依赖。
6. Root 与内联成员共享复杂遍历算法时，将其放入家族内部的公共生成辅助类。可参考 ImmutableQueueTraversal。

现有原始数组、列表以及配对、嵌套字典记录形状保留各自的存储投影。新的存储表示需要相应分析事实。注册 owner 本身不能证明布局、raw 传输或 wire 格式适用性。

## 元素形式

- scalar 处理已证明的内置原始标量。
- text 处理字符串及编译后的 profile。
- payload 处理具有完整规范化渲染模型、可展开的 LuminPackable 元素。
- forwarded 调用元素的具体生成 owner。递归依赖收集为嵌套集合发布子 owner。

注册时明确列出支持形式。缺失变体不能回退到其他家族的模板。

## 操作与支持代码

使用 FormatterOwnerFile 的四个操作回调。Root、serializer 入口、owner 转发及扩展入口由相同的操作选择控制，默认生成四种操作。

共享支持声明由实际使用它的操作决定，例如不可变集合候选缓存由二进制读取使用。

## 保持行为

保留 null 与空值 framing、元素顺序、比较器、构造、复用、失败时部分发布、游标所有权、检查及池归还。可选 runtime helper 的能力事实必须从引用的 runtime 分析得到，同时保留原有回退。

参考旧 emitter 及其完整生成结果。空格可以变化，移动操作体时保留执行顺序。

验证遵循任务要求和贡献指南。跳过构建、生成代码编译或运行时检查时，明确说明。

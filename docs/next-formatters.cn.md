# Next 生成器的 Formatter 契约

[English](next-formatters.md)

Next 将闭合类型分析为按值比较的 `FormatterModel`，其泛型参数递归描述元素操作。依赖图只在生成阶段存在，用于发布具体静态 owner。生成代码将调用者的编译选项和同一操作状态传给子 owner 的 `Write` 或 `Read`。

## 元素契约

`Raw` 表示独立值操作的原生存储契约。`RawCollection` 表示原有的连续元素存储契约。`Nullable<T>` 和 `KeyValuePair<TKey, TValue>` 的这两项事实可能不同。`Layout` 保存递归 ABI 字段、显式偏移、紧凑宽度和原生布局模型。容器按原有算法选择独立元素操作或连续块操作。

`GenericIdentity` 保留完整的嵌套泛型参数。元组、键值对、可空值、Lazy、分组和 Memory 的 owner 使用各参数自己的 Formatter 契约。`Tuple` 和 `ValueTuple` 保留第八个 `Rest` 参数。框架标量 owner 保留原有 JSON 表示。

标量、字符串和 LuminPackable 的特殊化模板继续可用。每个已注册家族都提供默认的元素 owner 转发路径。例如 `ConcurrentQueue<List<int[]>>`、`Dictionary<string, Tuple<MyClass, List<MyStruct>>>` 和 `Memory<Nullable<MyStruct>>`，其所引用的载荷正文仍需具备模型对应的实现。

## Unmanaged 存储

Unmanaged 分析保留声明的 `StructLayout` 策略，并在选择原生存储前解析闭合泛型参数。存储图中的 Custom 契约会排除原生复制路径。

- `Native` 保留完整的本机内存表示。
- `Packed` 通过保持 ABI 的存储镜像逐字段传输紧凑表示。
- `Tagged` 保留原有原生布局模型，连续载荷使用经过字段地址验证的 `LuminStorageTag`。独立的 portable 值保留紧凑字段表示。

元组分量和嵌套聚合保留实际字段。空聚合保留零宽度的 Packed 契约。Portable 块使用现有向量化传输和逐字段尾部。Reader 在分配、修改复用目标和推进游标之前执行检查。标量内核与 `LuminPackMarshal.CopyBlockUnaligned` 保留 32 位访问契约。

## 添加一个家族

1. 在 `FormatterRegistry` 注册开放类型定义和默认 renderer。
2. 在该家族目录实现遍历、构造和复用行为。子元素与键值操作使用上下文提供的操作。
3. 连续存储与独立元素操作的表示有区别时，明确描述连续存储契约。
4. 在 Analysis 中按完整公开签名检测可选运行时 API，将能力名称写入模型，并保留原有回退。
5. 保留旧实现的池归还、读取失败前缀、回调、比较器行为、null 覆盖和游标发布。
6. 经 `FormatterOwnerFile` 或 `SpecialOwnerFile` 发布操作，由 `GeneratedOperations` 控制保留项。

默认路径递归发布依赖。新增闭合元素组合会复用已有容器实现。注册表使用开放类型标识和能力名称，家族数量可以继续扩展。

## 载荷和管线边界

模型具备正文实现时才引用对应载荷 owner。`CanPublish` 在集合发布子调用前检查这一边界。跨程序集类型和泛型 Union 当前使用显式类型化注册边界。关闭注册时，相关操作保留抛出异常的行为。

Serializer facade、独立调用发现、静态入口绑定、泛型 Union 发布、编译输入策略和其余载荷正文协议仍需完成生成器集成。最终兼容性检查覆盖生成产物的消费者、新旧互读、已有目标和各条件运行时资产。

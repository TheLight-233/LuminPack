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

## 声明与载荷契约

Analysis 按旧有的 48 个诊断描述符执行声明校验，校验独立于正文的渲染覆盖情况。规则涵盖成员存储与名称、override 注解、回调可访问性与参数、构造函数选择、池租借、Union 根与分支、Custom 签名、编译选项、有界大小和 portable 存储。错误声明会阻止发布其可执行正文。诊断模型保存标识、参数和源码坐标，发布时将主位置和关联位置绑定到当前 Compilation，供 IDE 代码修复使用。

构造参数按大小写不敏感的成员名匹配。新建的构造型记录先将各成员解码到类型化局部变量，再执行构造。已有实例保留逐成员原地读取。托管 struct 保存构造、租借、回调和引用成员复用事实。Include 字段、readonly 字段和自动属性通过 Local 存储访问。继承镜像保留基类在前的独立声明、显式偏移和布局属性。volatile 标量存储排除在相邻字段块复制证明之外。

版本容忍模型生成描述符表及包含旧名称的名称键 switch。已知契约使用编译期 schema 常量，开放泛型成员则为每个闭合构造解析一次类型契约。按位置与按映射的读取路径共用成员读取方法。带长度前缀的成员使用单遍回填及带作用域的 schema 登记，简写记录携带序号。修订版 1 读取交给独立的兼容解码器。引用跟踪包裹所选记录体，保留显式 id、提前登记对象外壳、复用及紧凑回引路径。集合及子负载继续调用自己的具体静态 owner。

## 生成阶段的状态归属

Roslyn 符号保留在 Compilation 所属缓存或弱键分析缓存中，输出模型只保存可按值比较的事实。声明诊断、实际存储顺序和可选运行时能力按声明或闭合类型捕获一次。基础标量和 string 成员直接进入对应的分析分支。共享发布按批次累积依赖，随后统一去重和排序。

声明选项组合现有的无状态字符串、StandardFormat、JSON 深度和语法选项类型。非默认组件声明在载荷依赖图间去重。二进制写、二进制读、JSON 写和 JSON 读继续使用独立的操作掩码。

## 载荷和管线边界

模型具备正文实现时才引用对应载荷 owner。`CanPublish` 在集合发布子调用前检查这一边界。跨程序集类型和泛型 Union 当前使用显式类型化注册边界。关闭注册时，相关操作保留抛出异常的行为。

Serializer facade、独立调用发现、静态入口绑定、泛型 Union 发布及其缓存槽位、Custom 自动注册、旧有 unmanaged 根方法共享、编译输入限制和指针游标运行时发布仍需完成生成器集成。特殊化内联集合中的成员选项传递也仍需对齐。最终兼容性检查覆盖生成产物的消费者、新旧互读、已有目标和各条件运行时资产。

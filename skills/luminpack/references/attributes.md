# LuminPack 属性目录

命名空间：`LuminPack.Attribute`（`using LuminPack;` 通常已引入类型，特性位于 `LuminPack.Attribute`）。

## 类型级

| 特性 | 目标 | 说明 |
| --- | --- | --- |
| `[LuminPackable(GeneratorType g = Object)]` | class / struct / interface | 标记类型参与源码生成。`GeneratorType`：`Object`（默认）、`VersionTolerant`、`CircleReference`、`NonGenerator`、`Custom`。 |
| `[LuminPackUnion]` / `[LuminPackUnion(ushort tag, Type type)]` | interface / abstract class | 多态 union。无参形式自动收集 `[LuminPackable]` 派生类型；带参形式手动注册派生类型与 tag。tag 支持 0~65535，250 以下性能更佳。 |
| `[LuminPackWideTag]` | class / interface | 该类型启用宽 tag（union tag 超过 255 时使用）。 |
| `[LuminPackGeneratorOptions(mode, register = Auto)]` | 程序集（assembly） | 设置生成模式 `LuminPackGenerationMode`（Full/Medium/Light/Minimal）与 Register 模式 `LuminPackRegisterMode`（Auto/Disabled）。 |

## 成员级

| 特性 | 目标 | 说明 |
| --- | --- | --- |
| `[LuminPackOrder(uint order)]` | field / property | 显式指定成员顺序。`CircleReference` / `VersionTolerant` 模式下每个成员必填。 |
| `[LuminPackIgnore]` | field / property | 从序列化目标中排除。 |
| `[LuminPackInclude]` | field / property | 把私有成员提升为序列化目标。`netstandard2.1` 下嵌套类型私有字段不生效（见下）。 |
| `[LuminPackableObject]` | field / property | 不内联解析该成员，改经注册表派发到目标类型自己的 formatter（约损失 30% 性能）。用于生成器无法正确内联的嵌套类型。 |
| `[LuminPackDelegate(string instanceName, string methodName)]` | field / property | 成员序列化委托给某实例上的方法。 |
| `[LuminPackStaticDelegate(Type typeName, string methodName)]` | field / property | 成员序列化委托给静态方法。 |
| `[LuminPackSingletonDelegate(Type typeName, string singletonFieldName, string methodName)]` | field / property | 委托给单例字段上的方法；单参重载默认单例字段名为 `Instance`。 |
| `[LuminPackFixedLength(uint fixedLength)]` | field / property | 定长数组。 |
| `[LuminPackCompress]` | field / property | 对非托管数组/跨度使用 LZ 压缩。托管类型数组不可用（诊断 LuminPack034）。 |

## 方法与构造函数级

| 特性 | 目标 | 说明 |
| --- | --- | --- |
| `[LuminPackOnSerializing]` | method | 序列化前回调，static 或实例均可。 |
| `[LuminPackOnSerialized]` | method | 序列化后回调。 |
| `[LuminPackOnDeserializing]` | method | 反序列化前回调。 |
| `[LuminPackOnDeserialized]` | method | 反序列化后回调。 |
| `[LuminPackPoolRent]` | static method | 标记返回类型实例的 `Rent()` 方法，启用反序列化缓存池。用户自行实现池并负责 Return。 |
| `[LuminPackConstructor]` | constructor | 指定反序列化使用的构造函数（默认不依赖构造函数）。 |

## LuminMapper 映射特性（`LuminPack.Attribute`）

| 特性 | 目标 | 说明 |
| --- | --- | --- |
| `[LuminMapTo(typeof(TDest))]` | class / struct（源类型） | 自动路径。生成器自动产出 `{SourceTypeName}Mapper` 静态类：`ToDto` / `MapIntoDto` / `ToDtoArray` / `ToDtoList`。可重复标记实现一对多。可选 `MethodSuffix`、`GenerateCollectionMethods`。 |
| `[LuminMapper]` | static partial class | 手动路径。用户声明 `static partial` 方法，生成器实现方法体：`ToXxx(in TSource)`、`MapIntoXxx(in TSource, ref TDest)`、`ToXxxArray`、`ToXxxList`。 |
| `[LuminMapIgnore]` | field / property（目标类型） | 映射时跳过该成员。 |
| `[LuminMapFromMember("SourceMemberName")]` | field / property（目标类型） | 指定目标成员从不同名的源成员读取。 |
| `[LuminMapUsing(Type converterType, string methodName)]` | field / property（目标类型） | 目标成员用静态转换方法映射（参数为源字段类型，返回目标字段类型）。 |
| `[LuminMapCondition(Type conditionType, string methodName)]` | field / property（目标类型） | 静态条件 `bool Method(in TSource)`，返回 false 时目标成员保持默认值。 |
| `[LuminMapConstant(object? value)]` | field / property（目标类型） | 用编译期常量填充目标成员（仅 string/bool/int/long/float/double/null）。 |

## 常见组合

- 私有嵌套类型需要解析私有字段时：`[LuminPackInclude]` + `[LuminPackableObject]` 同时标注字段。
- 版本容忍 / 循环引用：类型标 `[LuminPackable(GenerateType.VersionTolerant / CircleReference)]`，每个成员标 `[LuminPackOrder]`。
- 压缩热数据数组：成员标 `[LuminPackCompress]`（配合 `LuminPackSerializer.Compress/Decompress` 或原生数组快速路径）。

## 手写方法示例

### 反序列化缓存池 [LuminPackPoolRent]

```csharp
// 标在静态方法上，返回该类型实例；生成器用此方法替代 new 进行反序列化
[LuminPackPoolRent]
public static SimpleClass Rent()
{
    return SimpleClassPool.Shared.Rent();   // 池由用户自行实现
}
```

### 委托成员 [LuminPackStaticDelegate] / [LuminPackSingletonDelegate]

```csharp
// 成员序列化交给外部类型的静态方法
[LuminPackStaticDelegate(typeof(MySerializer), nameof(MySerializer.WriteSpecial))]
public SpecialData data;
```

```csharp
// 成员序列化交给单例实例上的方法（单参重载默认单例字段名 Instance）
[LuminPackSingletonDelegate(typeof(MySerializer), nameof(MySerializer.Instance), nameof(MySerializer.WriteSpecial))]
public SpecialData data;
```

委托方法的签名与对应成员读写一致：`WriteSpecial(ref LuminPackWriter writer, in SpecialData value)` 等，由你手写并保持与字段类型匹配。

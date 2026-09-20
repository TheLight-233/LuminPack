---
name: luminpack
description: LuminPack 高性能序列化库的使用说明。覆盖 LuminPackable 标记、序列化属性、LuminPackSerializer API、生成模式配置、多态 union、版本容忍、自定义格式化器、LuminMapper 映射与压缩。当编写或调试使用 LuminPack 的代码时使用。
---

# LuminPack 使用说明

LuminPack 是面向 Unity 存档、网络传输的高性能二进制与 JSON 序列化库，支持 `netstandard2.1` / `net8.0` 至 `net11.0`。核心设计：无反射、无运行时委托/字典缓存，热路径全部是源码生成器生成的直接方法调用，JIT 可内联，序列化过程不产生虚调用与托管分配。

本 Skill 是 LuminPack 的使用说明书，目的是让 AI 写出正确使用 LuminPack 的代码，不涉及库内部实现，也不规定你的工作流程。

## 快速开始

1. 用 `[LuminPackable]` 标记要序列化的 class / struct / record / interface。
2. 序列化与反序列化走 `LuminPackSerializer`：

```csharp
var buffer    = LuminPackSerializer.Serialize(item);              // byte[]
var json      = LuminPackSerializer.SerializeJson(item);          // string
var result    = LuminPackSerializer.Deserialize<Person>(buffer);  // 泛型便捷入口
var jsonResult = LuminPackSerializer.DeserializeJson<Person>(json);
```

3. 高吞吐场景用可复用 `LuminBufferWriter`（非托管内存，**必须 Dispose**）。

## 使用约定（遵守才能正确工作）

- 成员按**声明顺序**序列化，继承类型按 **父→子**；反序列化必须保持同样顺序与类型。
- 对象成员数上限 `0~249`；`255` = null object；集合长度 `-1` = null。
- 二进制端序一律 **Little Endian**。
- JSON 与二进制是两套独立协议，不能把 `Serialize` 的结果交给 `DeserializeJson`（反之亦然）。
- unmanaged struct（不含引用类型）直接复制内存，性能最高。
- 默认生成模式（`GenerateType.Object`）：可加成员、不可删成员/改顺序/改类型/改名。
- `VersionTolerant` / `CircleReference` 模式下**每个成员必须标注 `[LuminPackOrder]`**。
- 反序列化不依赖构造函数，可随意定义构造函数。

## 属性速查

完整清单见 [references/attributes.md](references/attributes.md)。高频属性：

- `[LuminPackable(GeneratorType.Object | VersionTolerant | CircleReference | Custom)]`
- `[LuminPackOrder(uint)]` / `[LuminPackIgnore]` / `[LuminPackInclude]` / `[LuminPackableObject]`
- `[LuminPackUnion(ushort tag, Type)]`（多态，tag 0~65535，250 以下性能更佳）
- `[LuminPackCompress]`（非托管数组压缩）、`[LuminPackFixedLength]`
- 回调：`[LuminPackOnSerializing/OnSerialized/OnDeserializing/OnDeserialized]`
- 缓存池：`[LuminPackPoolRent]`

## API 速查

`LuminPackSerializer` 入口、Option、Register、压缩、加密、低级 Writer/Reader 的签名见 [references/api.md](references/api.md)。需要手写方法实现时（Custom formatter、Register 注册、跨程序集多态、缓存池、委托成员），参考该文件与 [references/attributes.md](references/attributes.md) 中的完整示例代码。

核心 API：

```csharp
// 二进制
byte[] Serialize(in T value, LuminPackSerializerOption? option = null);
void   Serialize(in T value, LuminBufferWriter writerBuffer);   // 复用缓冲
int    Deserialize(ReadOnlySpan<byte> buffer, ref T value, ...);
T      Deserialize<T>(ReadOnlySpan<byte> buffer, ...);

// JSON
string SerializeJson(T value, LuminPackSerializerOption? option = null);
int    DeserializeJson(string buffer, ref T value, ...);

// 压缩 / 解压
byte[] Compress(ReadOnlySpan<byte> source);
byte[] Decompress(ReadOnlySpan<byte> source);
```

## 生成模式配置（控制编译产物大小）

生成器默认 `Full` 模式为每个类型生成代码。大型项目可用 `Medium` / `Light` / `Minimal` 剪枝，只生成真正会被序列化的类型：

- csproj：`<LuminPackGenerationMode>Light</LuminPackGenerationMode>` + `<CompilerVisibleProperty Include="LuminPackGenerationMode" />`
- 或程序集特性：`[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]`

注意：`Light` / `Minimal` 会剪掉无法静态证明可达的序列化（反射、跨程序集等），此类场景请用 `Full` / `Medium`。剪枝只影响生成代码量，不影响运行期性能。

## 性能提示

- 热路径优先用零分配的写法：复用 `LuminBufferWriter`、避免装箱。
- 反序列化缓存池：静态方法标 `[LuminPackPoolRent]` 提供 `Rent()`；用户自行实现池并负责 `Return`。
- 手写自定义 formatter 时，用 writer/reader 的 `WriteValue/ReadValue`、`WriteUnmanagedArray`、`WriteSpan` 等原语。

## 常见陷阱

- `netstandard2.1` 下嵌套类型的私有字段 `[LuminPackInclude]` 不生效，改用 `[LuminPackableObject]`（约损失 30% 性能，仅必要时用）。
- `Register<T>` / 基类 `Register<TMember>` 使用函数指针，调用处与项目都需要 `AllowUnsafeBlocks`。
- `GeneratorType.Custom` 手写方法必须 `static`，签名精确匹配三种 Register 重载之一（见 api.md），否则编译报 LuminPack100~108。
- Custom 类型作为其他 `[LuminPackable]` 的字段时，字段要标 `[LuminPackableObject]`。
- 字符串编码 `UTF8` / `UTF16`、记录方式 `Length` / `Token` 在序列化与反序列化两侧必须一致。
- JSON 只保证有限浮点；`NaN` / `±Infinity` 序列化抛异常，反序列化越界数字被拒绝。
- `LuminBufferWriter` 使用非托管内存，用完必须调用 `Dispose()`。

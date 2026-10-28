# 🪶 生成模式

[English](generation-modes.md) · [返回 README](../README.cn.md)

默认情况下，生成器为它观察到的每个类型生成代码。大型项目可以把生成范围裁剪到真正会被序列化的类型，减小程序集体积、加快编译。

| 模式 | 生成的格式化器 | `LuminPackSerializer` 具体重载 |
| --- | --- | --- |
| `Full`（默认） | 所有观察到的类型。 | 每个保留类型的所有重载形态。 |
| `Medium` | 可达类型，加上所有 `public` 类型（对类库更保守）。 | 所有重载形态。 |
| `Light` | 从 `LuminPackSerializer.*` 调用点以及所有 `[LuminPackable]` 成员图可达的类型。 | 只生成实际调用到的重载形态。 |
| `Minimal` | 只有从 `LuminPackSerializer.*` 调用点可达的类型。未使用的 `[LuminPackable]` 类型也会被裁掉。 | 只生成实际调用到的重载形态。 |

可达性的起点：

- 每个 `LuminPackSerializer.*` 调用及其传入的类型；
- 泛型包装方法：`void Save<T>(T x) => LuminPackSerializer.Serialize(x);` 以 `Save<Player>()` 调用时，`Player` 成为根；
- 递归展开类型实参、数组元素和成员类型；
- 可达 Union 根的所有 case。

## 选择模式

使用 MSBuild 属性：

```xml
<PropertyGroup>
  <LuminPackGenerationMode>Light</LuminPackGenerationMode>
</PropertyGroup>
<ItemGroup>
  <CompilerVisibleProperty Include="LuminPackGenerationMode" />
</ItemGroup>
```

必须同时添加 `CompilerVisibleProperty`，生成器才能读到这个属性。

或使用程序集特性：

```csharp
[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Light)]
```

两种方式同时存在时，取更激进的一档。特性的第二个参数 `LuminPackRegisterMode.Disabled` 还可以关闭生成的 `Register` API。

## 注意事项

- 在 `Light` 和 `Minimal` 下，生成器无法证明的序列化（反射、`typeof` 驱动的代码、来自其他程序集的调用）会在运行时像没有格式化器的类型一样失败。这类项目请使用 `Full` 或 `Medium`。
- `Minimal` 假定所有序列化都通过本程序集里的静态 `LuminPackSerializer` 调用完成，否则请使用 `Light`。
- 裁剪不会改变数据格式或类型所用的格式化器，只决定哪些代码存在。
- 对生成范围之外的类型进行泛型 `LuminPackSerializer` 调用需要[注册](custom-formatters.cn.md#runtime-registration)。

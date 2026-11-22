# LuminPack for Java

[English](../../src/LuminPack.Java/README.md)

Java 运行时读写 LuminPack 二进制，以及 UTF-8 或 UTF-16LE JSON。生成的 Java 类持有根 codec 和从 C# 模型导出的契约。[跨语言契约](../portable-runtime.cn.md)

## 构建并生成绑定

使用 JDK 17 或更高版本。以下命令在仓库根目录执行，使用共享指南中的 `Example.Message` 模型生成绑定，并与运行时源码一起编译。

```sh
dotnet run --project tools/LuminPack.Codegen -c Release -- generate --language java --package example.generated .tmp/contracts.json Example.Message .tmp/Message.java
javac --release 17 -encoding UTF-8 -d .tmp/java-classes src/LuminPack.Java/src/main/java/luminpack/*.java .tmp/Message.java
```

输出文件名决定 Java 类名。默认包名为 `luminpack.generated`。将编译产物加入应用的 classpath，也可以将运行时源码和生成文件直接加入应用构建。

## 读写数据

`Message.CODEC` 接收以导出成员名为键的模型。二进制方法使用投影后的操作配置。JSON 方法可显式传入 `LuminPack.UTF8` 或 `LuminPack.UTF16`。

```java
import example.generated.Message;
import java.util.List;
import java.util.Map;
import luminpack.LuminPack;

var model = Map.of("Id", 7, "Text", "ready", "Measurements", List.of(2, 5));
byte[] binary = Message.CODEC.writeBinary(model);
Object copy = Message.CODEC.readBinary(binary);
byte[] json = Message.CODEC.writeJson(copy, LuminPack.UTF8);
Object fromJson = Message.CODEC.readJson(json, LuminPack.UTF8);
```

| 契约值 | Java 值 |
| --- | --- |
| 记录或已建模的结构体 | 以导出成员名为键的 `Map<String, Object>` |
| 数组、List、Pair、Tuple | List。符合条件的基本类型连续块也以 List 形式提供。 |
| 字典 | Map。需要复现对端条目顺序时使用 `LinkedHashMap`。 |
| 矩形数组 | 包含 `lengths`、`lowerBounds` 和行优先 `values` 的 Map。 |
| 64 位无符号整数 | `BigInteger`。原生整数使用导出契约指定的宽度。 |
| 声明的 Union 分支 | `LuminPack.UnionValue`，将无符号 32 位分支哈希存入 `long`。 |
| 注册的 Union 分支 | `LuminPack.RegisteredUnionValue`，携带注册 tag 和数据。 |

引用跟踪开启时，为重复出现的节点复用同一个 Map 实例。codec 保存契约，每次调用独立管理该文档的引用跟踪和 Version 状态。

## 加载契约和绑定应用 codec

通过 `LuminPack.fromManifest(Files.readString(path))` 加载 `project` 输出。生成的绑定已包含初始化的 codec。`withCustom(type, binding)` 和 `withUnionCase(unionType, tag, binding)` 返回新的 codec。使用完整契约标识并实现 `LuminPack.CustomCodec`，使二进制和 JSON 数据与对端一致。运行时处理 Union tag 后会调用对应的数据回调。

## 文本和基本类型快路径

基础运行时使用有边界检查的机器字文本操作和连续基本类型存储。可选的 Vector API 可加速长 ASCII 和 Token 扫描。使用包含 `jdk.incubator.vector` 的 JDK 编译该实现，并将 `src/vector/resources` 下的服务元数据加入同一个 classpath。

```sh
javac --add-modules jdk.incubator.vector -cp .tmp/java-classes -d .tmp/java-classes src/LuminPack.Java/src/vector/java/luminpack/VectorStringKernels.java
cp -R src/LuminPack.Java/src/vector/resources/META-INF .tmp/java-classes/
java --add-modules jdk.incubator.vector -cp .tmp/java-classes example.Main
```

## 验证互通

```sh
python tests/LuminPack.Tests.Portable/verify.py --language java
python tests/LuminPack.Tests.Portable/verify.py --language java --suite strings --java-vector
```

<div align="center">

# 🌐 LuminPack.AspNetCore

**为 ASP.NET Core MVC 与 Minimal API 提供二进制请求体和响应体**

![Optional](https://img.shields.io/badge/module-optional-8B5CF6?style=flat-square) ![net8.0](https://img.shields.io/badge/target-net8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white) ![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=flat-square) ![Source only](https://img.shields.io/badge/NuGet-source%20only-lightgrey?style=flat-square)

[English](README.md) · **简体中文**

属于 [LuminPack](../../README.cn.md) · [全部平台模块](../LuminPack.Platforms/README.cn.md)

[特性](#-特性) · [MVC](#-mvc) · [Minimal API](#-minimal-api) · [错误与限制](#-错误与限制) · [HTTP 值类型](#-http-值类型) · [构建](#-构建)

</div>

---

可选的 ASP.NET Core 集成，基于应用自行选定的具体 LuminPack 操作。Core 不依赖 ASP.NET。

## ✨ 特性

- 📥 **MVC** 二进制输入与输出格式化器
- ⚡ **Minimal API** 请求读取与结果
- 🧾 **HTTP 值编解码器**，覆盖 `StringValues`、`StringSegment`、`HostString`、`PathString`、`QueryString` 与 `FragmentString`
- 🔒 **没有反射**——不使用反射序列化器，也不会根据输入激活 CLR 类型

## 🧱 MVC

只注册你显式选定的 DTO：

```csharp
using LuminPack;
using LuminPack.AspNetCore;

var codec = new LuminPackHttpCodec<Message>(
    value => LuminPackSerializer.Serialize(value),
    bytes =>
    {
        Message? value = null;
        LuminPackSerializer.Deserialize(bytes.Span, ref value);
        return value!;
    });

builder.Services.AddControllers().AddLuminPack(options =>
{
    options.MaximumBodyBytes = 1024 * 1024;
    options.Add(codec);
});
```

- 应用对自己的 DTO 和具体的序列化调用运行现有生成器。
- 现有 JSON 格式化器保持默认优先级。
- 客户端通过 `Accept: application/x-luminpack` 请求二进制输出；二进制输入使用相同的 `Content-Type`。
- 两端的序列化选项必须保持完全一致。

## ⚡ Minimal API

请求与响应入口使用同一个编解码器：

```csharp
app.MapPost("/messages", async (HttpRequest request) =>
{
    Message value = await request.ReadLuminPackAsync(codec);
    return LuminPackHttp.Result(value, codec);
});
```

## 🚦 错误与限制

请求读取器会拒绝不支持的媒体类型，对已知长度以及未知长度/分块编码的请求体都做限制，响应 `RequestAborted` 和应用取消，并且不会关闭请求流。

| 情况 | 结果 |
| --- | --- |
| 请求体超过上限 | 状态码为 **413** 的 `BadHttpRequestException` |
| 不支持的媒体类型 | **415** |
| 已分类的无效负载 | **400** |

- **Minimal API**——使用应用常规的 HTTP 异常边界。
- **MVC**——已分类的解析失败会转为模型状态错误；应用异常保持原样。错误消息不包含解析器细节。
- **分类器**——默认识别 Core 的越界/记录异常以及 `FormatException`、`InvalidDataException` 和 `EndOfStreamException`；其他应用异常会继续抛出。编解码器构造函数可以接收显式分类器，用来识别额外的应用解析错误。
- **完整记录**——应用读取器必须校验完整记录；适配器无法推断任意生成委托实际消费了多少字节。显式的平台根编解码器本身已会拒绝尾部多余字节。
- **响应**——先成功完成序列化，再发布响应头和响应体。

> [!WARNING]
> 默认的 **1 MiB** 上限针对的是编码后的请求体，并不是解码后 DTO 的内存分配、递归或 CPU 预算——请选择合适的 schema 和应用限制，仅靠请求大小无法保证任意输入是安全的。该传输层不提供身份验证、加密、压缩或 HTTP 类型标识。

## 🧾 HTTP 值类型

| 类型 | 线格式记录 |
| --- | --- |
| `StringValues` | 一个计数加上完整的 Core 字符串，保留顺序、null 元素和空字符串。其私有的 object/string/array 存储不会被序列化。每条记录最多 65,536 个值。 |
| `StringSegment` | 只编码选中的片段，而不是整个底层字符串 |
| `HostString` · `PathString` · `QueryString` · `FragmentString` | 公开的逻辑 `Value`，包括 null 和空值 |

二进制字符串使用声明默认的 Core 配置。使用 `LuminAspNetCore.Register()` 初始化这些值编解码器；直接调用编解码器无需注册。生成 DTO 的字段可以按照[共享适配器](../LuminPack.Platforms/README.cn.md)中的说明使用 `LuminPackValue<T>`。

## 🔨 构建

基于 ASP.NET Core 共享框架和已经构建好的 **net8.0** Core DLL 进行构建。该项目绝不会重新构建 Core 或生成器；它是可选的源码项目，而不是已发布的 NuGet 包。

针对性测试会发起真实的 MVC 与 Minimal API 请求：内容协商、保持不变的 JSON 行为、格式错误/超大/未知长度的输入、取消、回调身份以及生成的 Custom 请求头。

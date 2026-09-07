namespace LuminPack.Code;

public enum GeneratorType : byte
{
    Object,
    VersionTolerant,
    CircleReference,
    NonGenerator,

    /// <summary>用户手写静态序列化方法，生成器不生成任何扩展方法，只做校验与自动注册。</summary>
    Custom
}
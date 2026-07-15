namespace LuminPack.Interface;

#if NET8_0_OR_GREATER

public interface IFixedSizeLuminPackable
{
    static abstract int Size { get; }
}

#endif
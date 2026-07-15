using LuminPack.Core;

namespace LuminPack.Internal;

public delegate void LuminUnionWriteDelegate(ref LuminPackWriter writer, scoped in object? value);

public delegate void LuminUnionReadDelegate(ref LuminPackReader reader, scoped in object? value);

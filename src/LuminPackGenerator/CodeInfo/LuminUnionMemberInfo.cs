using System;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code;

public readonly record struct LuminUnionMemberInfo
{
    public readonly ushort Id;
    public readonly INamedTypeSymbol Type;
    public readonly bool CanGenerateDispatch;

    public LuminUnionMemberInfo(ushort id, INamedTypeSymbol type, bool canGenerateDispatch = false)
    {
        Id = id;
        Type = type;
        CanGenerateDispatch = canGenerateDispatch;
    }
}

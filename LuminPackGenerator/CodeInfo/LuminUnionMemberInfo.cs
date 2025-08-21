using System;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code;

public readonly record struct LuminUnionMemberInfo
{
    public readonly ushort Id;
    public readonly INamedTypeSymbol Type;

    public LuminUnionMemberInfo(ushort id, INamedTypeSymbol type)
    {
        Id = id;
        Type = type;
    }
}
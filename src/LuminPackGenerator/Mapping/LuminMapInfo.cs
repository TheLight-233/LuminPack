using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Mapping;

// ─────────────────────────────────────────────────────────────────────────────
//  公共绑定模型（两条路径共用）
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>目标类型单个成员的映射绑定描述</summary>
public sealed class LuminMapFieldBinding
{
    public string  DestMemberName      { get; set; } = "";
    public bool    DestIsProperty      { get; set; }
    /// <summary>
    /// 直接写入生成代码的源侧表达式。
    /// 示例："source.Name" / "global::NS.Conv.Parse(source.Raw)" / "true"
    /// </summary>
    public string  SourceExpression    { get; set; } = "";
    public bool    IsIgnored           { get; set; }
    /// <summary>非 null 时生成 if (condition) dest.X = expr;</summary>
    public string? ConditionExpression { get; set; }
    public bool    SourceMemberFound   { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ① 自动路径数据模型（[LuminMapTo] 驱动）
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 单个 [LuminMapTo(typeof(TDest))] 声明的分析结果。
/// 包含从源类型到目标类型的完整字段绑定。
/// </summary>
public sealed class AutoMapPairInfo
{
    public string SourceTypeFullName      { get; set; } = "";
    public string DestTypeFullName        { get; set; } = "";
    public bool   DestIsValueType         { get; set; }
    public bool   DestHasDefaultCtor      { get; set; }
    /// <summary>
    /// 方法名后缀（推断或用户覆盖）。
    /// 生成方法名: To{Suffix} / MapInto{Suffix} / To{Suffix}Array / To{Suffix}List
    /// </summary>
    public string MethodSuffix            { get; set; } = "";
    public bool   GenerateCollectionMethods { get; set; } = true;
    public List<LuminMapFieldBinding> Bindings { get; } = new();
}

/// <summary>
/// 一个源类型上所有 [LuminMapTo] 的聚合，
/// 生成器为每个实例产出一个 {SourceName}Mapper.g.cs。
/// </summary>
public sealed class AutoMapSourceInfo
{
    public string SourceClassName   { get; set; } = "";
    public string SourceNamespace   { get; set; } = "";
    public string SourceFullName    { get; set; } = "";
    public bool   IsGlobalNamespace { get; set; }
    public bool   IsUnityProject    { get; set; }
    public List<AutoMapPairInfo> Pairs { get; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
//  ② 手动路径数据模型（[LuminMapper] partial class 驱动）
// ─────────────────────────────────────────────────────────────────────────────

public enum CollectionMapKind { None, Array, List }

public sealed class LuminMapMethodInfo
{
    public string              MethodName         { get; set; } = "";
    public string              SourceTypeFullName { get; set; } = "";
    public string              DestTypeFullName   { get; set; } = "";
    public bool                SourceIsIn         { get; set; }
    public bool                IsCreateNew        { get; set; }
    public bool                DestIsValueType    { get; set; }
    public bool                DestHasDefaultCtor { get; set; }
    public List<LuminMapFieldBinding> Bindings    { get; } = new();
    public CollectionMapKind   CollectionKind     { get; set; } = CollectionMapKind.None;
    public LuminMapMethodInfo? ElementMethod      { get; set; }
}

public sealed class LuminMapperClassInfo
{
    public string  ClassName        { get; set; } = "";
    public string  ClassNamespace   { get; set; } = "";
    public string  ClassFullName    { get; set; } = "";
    public bool    IsGlobalNamespace { get; set; }
    public bool    IsUnityProject   { get; set; }
    public List<LuminMapMethodInfo> Methods { get; } = new();
}

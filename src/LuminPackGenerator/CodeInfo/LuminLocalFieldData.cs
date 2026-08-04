namespace LuminPack.Code;

public sealed class LuminLocalFieldData
{
    public string TypeName;
    public string Name;
    public string Identifier =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ||
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + Name
            : Name;
    public int filedOffset;
    public bool IsValue;
}

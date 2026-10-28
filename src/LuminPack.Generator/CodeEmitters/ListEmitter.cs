namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>Provides list emitter functionality.</summary>
public static partial class ListEmitter

{

    /// <summary>Obtains the collection base type for the supplied input.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetCollectionBaseType(string typeName)
    {
        int separator = typeName.IndexOf('<');
        return separator < 0 ? typeName : typeName.Substring(0, separator);
    }
}

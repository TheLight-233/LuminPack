using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides interface read emitter shape regression functionality.</summary>
internal static class InterfaceReadEmitterShapeRegression
{
    /// <summary>Verifies run.</summary>
    public static void Run()
    {
        string listRead = Generate(
            "global::System.Collections.Generic.IList<global::System.String>",
            InterfaceListEmitter.GenerateDeserializeCode);
        AssertContains(
            listRead,
            "reader.CreateFreshListArray<global::System.String>(length, out var list)",
            "IList<T> must fill a fresh List<T> backing array without List.Add bookkeeping.");
        AssertContains(
            listRead,
            "Unsafe.Add(ref first, i)",
            "IList<T> must retain a bounds-check-free concrete element loop.");
        AssertDoesNotContain(
            listRead,
            ".Add(item)",
            "IList<T> reintroduced per-element List.Add/version/capacity work.");

        AssertDictionaryCapacity(
            "global::System.Collections.Generic.IDictionary<global::System.String, global::System.Int32>",
            InterfaceDictionaryEmitter.GenerateDeserializeCode,
            "IDictionary<TKey, TValue>");
        AssertDictionaryCapacity(
            "global::System.Collections.Generic.IReadOnlyDictionary<global::System.String, global::System.Int32>",
            InterfaceReadOnlyDictionaryEmitter.GenerateDeserializeCode,
            "IReadOnlyDictionary<TKey, TValue>");
    }

    /// <summary>Verifies assert dictionary capacity.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="generator">The generator used by this operation.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertDictionaryCapacity(
        string typeName,
        Action<LuminLocalFieldData, StringBuilder> generator,
        string scenario)
    {
        string read = Generate(typeName, generator);
        AssertContains(
            read,
            "Dictionary<global::System.String, global::System.Int32>(length)",
            scenario + " must preallocate from its validated collection header.");
        AssertContains(
            read,
            "dict.Add(k!, v);",
            scenario + " must retain Add so duplicate-key behavior remains unchanged.");
    }

    /// <summary>Captures the source produced by the code emitter under test.</summary>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="generator">The generator used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string Generate(
        string typeName,
        Action<LuminLocalFieldData, StringBuilder> generator)
    {
        var builder = new StringBuilder();
        generator(new LuminLocalFieldData { TypeName = typeName }, builder);
        return builder.ToString();
    }

    /// <summary>Verifies assert contains.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertContains(string text, string expected, string message)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }

    /// <summary>Verifies assert does not contain.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <param name="unexpected">The unexpected used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertDoesNotContain(string text, string unexpected, string message)
    {
        if (text.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }
}

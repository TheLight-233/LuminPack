using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerIssueRegression
{
    /// <summary>Checks fixed cursor eligibility and ordinary fallbacks when a case belongs to more than one Union.</summary>
    /// <param name="references">Platform and runtime references.</param>
    private static void VerifyFixedInterfaceUnion(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;

            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]

            [LuminPackable, LuminPackUnion(1, typeof(CursorBoth)), LuminPackUnion(2, typeof(CursorCallback))]
            public partial interface ICursorFixed { }

            [LuminPackable, LuminPackUnion(7, typeof(CursorBoth)), LuminPackUnion(8, typeof(CursorCallback))]
            public partial interface ICursorOther<T> { }

            [LuminPackable]
            public sealed partial class CursorBoth : ICursorFixed, ICursorOther<int>
            {
                public int Number;
            }

            [LuminPackable]
            public sealed partial class CursorCallback : ICursorFixed, ICursorOther<int>
            {
                public static int Calls;
                public int Number;

                [LuminPackOnSerializing]
                public static void Before() => Calls++;
            }

            public static class Entry
            {
                public static void Check()
                {
                    var fixedValue = new CursorBoth { Number = 11 };
                    var callbackValue = new CursorCallback { Number = 22 };
                    List<ICursorFixed> exact = [fixedValue, callbackValue, null];
                    List<ICursorOther<int>> generic = [fixedValue, callbackValue, null];
                    byte[] expected = Convert.FromHexString("030000003466648F0B000000180EFB471600000000000000");
                    byte[] exactBytes = LuminPackSerializer.Serialize(exact);
                    byte[] genericBytes = LuminPackSerializer.Serialize(generic);
                    if (!exactBytes.AsSpan().SequenceEqual(expected) || !genericBytes.AsSpan().SequenceEqual(expected))
                        throw new Exception("Mixed-root Union wire changed: " + Convert.ToHexString(exactBytes) + " / " + Convert.ToHexString(genericBytes));
                    if (CursorCallback.Calls != 2)
                        throw new Exception("An interface Union skipped or repeated its serialization callback.");

                    List<ICursorFixed> exactCopy = null;
                    List<ICursorOther<int>> genericCopy = null;
                    LuminPackSerializer.Deserialize(exactBytes, ref exactCopy);
                    LuminPackSerializer.Deserialize(genericBytes, ref genericCopy);
                    if (exactCopy is not [CursorBoth { Number: 11 }, CursorCallback { Number: 22 }, null] ||
                        genericCopy is not [CursorBoth { Number: 11 }, CursorCallback { Number: 22 }, null])
                        throw new Exception("A mixed-root Union changed the payload or case order on read.");
                }
            }
            """;

        foreach (string mode in new[] { "Full", "Minimal" })
            Execute(references, source.Replace("MODE", mode, StringComparison.Ordinal), "FixedInterfaceUnion" + mode, InspectFixedInterfaceUnion);
    }

    /// <summary>Checks that the exact interface root alone fuses the fixed arm's tag and payload.</summary>
    /// <param name="result">The generated consumer source.</param>
    private static void InspectFixedInterfaceUnion(GeneratorDriverRunResult result)
    {
        var owners = result.GeneratedTrees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()).ToArray();
        var fixedArms = BinaryCaseWriters("ICursorFixedFormatter");
        var genericArms = BinaryCaseWriters("ICursorOtherOfTFormatter");
        if (fixedArms.Length != 2 || !UsesFixedCursor(fixedArms[0]) || UsesFixedCursor(fixedArms[1]))
            throw new InvalidOperationException("The exact interface root did not limit its cursor path to the fixed case.");
        if (genericArms.Length != 2 || genericArms.Any(method => UsesFixedCursor(method) ||
            !method.Body!.ToString().Contains("writer.EnsureAdditionalCapacity(4)", StringComparison.Ordinal)))
            throw new InvalidOperationException("The same case changed the generic interface root's fallback path.");

        MethodDeclarationSyntax[] BinaryCaseWriters(string name) => owners.Where(owner => owner.Identifier.ValueText == name)
            .SelectMany(owner => owner.Members.OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText.StartsWith("WriteCase", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[0].Type!.ToString().EndsWith("LuminPackWriter", StringComparison.Ordinal)).ToArray();

        static bool UsesFixedCursor(MethodDeclarationSyntax method) => method.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(call => call.Expression is MemberAccessExpressionSyntax access &&
                (access.Name.Identifier.ValueText.StartsWith("SerializeUnionAt", StringComparison.Ordinal) ||
                    access.Name.Identifier.ValueText.StartsWith("WriteAt", StringComparison.Ordinal)));
    }
}

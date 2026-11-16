using System;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Emits the common ordered integer run for the legacy and model-based object readers.</summary>
internal static class GeneratedJsonInt32Runs
{
    private const int MinimumFields = 3;
    private const int MaximumFields = 16;

    /// <summary>Checks that the consuming project references a Core with managed cursor run support.</summary>
    /// <param name="compilation">The compilation selecting the runtime asset.</param>
    /// <returns>Whether the new generator-only reader entry is present.</returns>
    internal static bool Available(Compilation compilation)
    {
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader");
        if (reader == null)
            return false;
        foreach (var item in reader.GetMembers("TryBeginGeneratedInt32Run"))
        {
            if (item is IMethodSymbol { IsStatic: false, Arity: 1, DeclaredAccessibility: Accessibility.Public } method &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_Boolean &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[1].RefKind == RefKind.Out)
                return true;
        }
        return false;
    }

    /// <summary>Finds bounded contiguous runs whose fields can be published without leaving the reader.</summary>
    /// <param name="count">The number of members in the analyzed wire order.</param>
    /// <param name="eligible">Whether each member is a directly writable nonvolatile integer.</param>
    /// <returns>Run lengths indexed by start ordinal, or null when no run is selected.</returns>
    internal static int[] Find(int count, Func<int, bool> eligible)
    {
        int[] runs = null;
        int first = 0;
        while (first < count)
        {
            int end = first;
            while (end < count && eligible(end))
                end++;
            if (end - first >= MinimumFields)
            {
                runs ??= new int[count];
                runs[first] = end - first;
            }
            first = end + 1;
        }
        return runs;
    }

    /// <summary>Writes a bounded run and publishes only the consumed prefix before ordinary lookup resumes.</summary>
    /// <param name="emit">The destination for the generated statements.</param>
    /// <param name="first">The ordinal of the run's first field.</param>
    /// <param name="count">The number of fields in the run.</param>
    /// <param name="name">The wire name of the member at an ordinal.</param>
    /// <param name="target">The original direct field target at an ordinal.</param>
    /// <param name="presence">The ordinary assignment marking the field as present.</param>
    /// <param name="profile">The explicit profile type arguments, or an empty string before the legacy projection.</param>
    internal static void Emit(Action<string> emit, int first, int count, Func<int, string> name, Func<int, string> target,
        Func<int, string> presence, string profile)
    {
        // A full chunk normally falls into the next one. A miss skips subsequent chunks and resumes normal lookup.
        for (int start = first; start < first + count; start += MaximumFields)
        {
            if (start != first)
            {
                emit("if (nextPropertyIndex == " + start.ToString(CultureInfo.InvariantCulture) + ")");
                emit("{");
            }
            EmitChunk(emit, start, Math.Min(first + count - start, MaximumFields), name, target, presence, profile);
            if (start != first)
                emit("}");
        }
    }

    /// <summary>Emits the bounded local cursor operations for one generated chunk.</summary>
    /// <param name="emit">The destination for the generated statements.</param>
    /// <param name="first">The start ordinal of the chunk.</param>
    /// <param name="count">The number of fields the chunk covers.</param>
    /// <param name="name">The wire name of the member at an ordinal.</param>
    /// <param name="target">The original direct field target at an ordinal.</param>
    /// <param name="presence">The ordinary assignment marking the field as present.</param>
    /// <param name="profile">The reader's generic profile arguments.</param>
    private static void EmitChunk(Action<string> emit, int first, int count, Func<int, string> name, Func<int, string> target,
        Func<int, string> presence, string profile)
    {
        // Keep the out variable local to this chunk, including when another run is emitted in a switch arm.
        emit("{");
        emit("if (reader.TryBeginGeneratedInt32Run" + profile + "(" + (first == 0 ? "true" : "false") + ", out int luminJsonRemaining))");
        emit("{");
        emit("int luminJsonWindow = luminJsonRemaining;");
        emit("ref byte luminJsonCursor = ref reader.Cursor;");
        emit("do");
        emit("{");
        for (int index = first; index < first + count; index++)
        {
            string declaration = index == first ? "int " : string.Empty;
            string output = index == first ? "int " : string.Empty;
            emit(declaration + "luminJsonConsumed = global::LuminPack.Core.LuminPackJsonReader.ReadGeneratedInt32(ref luminJsonCursor, luminJsonRemaining, " +
                GeneratedJsonProperty.Arguments(name(index), separated: index != 0) + ", isUtf8, out " + output + "luminJsonNumber);");
            emit("if (luminJsonConsumed == 0) break;");
            emit(target(index) + " = luminJsonNumber;");
            emit(presence(index));
            emit("nextPropertyIndex = " + (index + 1).ToString(CultureInfo.InvariantCulture) + ";");
            emit("luminJsonRemaining -= luminJsonConsumed;");
            if (index + 1 < first + count)
                emit("luminJsonCursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref luminJsonCursor, (nint)(uint)luminJsonConsumed);");
        }
        emit("}");
        emit("while (false);");
        emit("reader.CompleteGeneratedInt32Run" + profile + "(luminJsonWindow - luminJsonRemaining);");
        emit("}");
        emit("}");
    }
}

using System.Reflection;
using System.Reflection.Emit;

/// <summary>Prints concrete Serializer IL and method-body metadata from a retained build image.</summary>
internal static class SerializerIlInspection
{
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes)
        .GetFields(BindingFlags.Static | BindingFlags.Public)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => (ushort)opcode.Value);

    /// <summary>Writes selected binary Serializer methods without executing benchmark operations.</summary>
    /// <param name="directory">A complete benchmark output directory.</param>
    /// <param name="payload">A case-sensitive fragment of the concrete payload type name.</param>
    /// <param name="methodName">An exact method name, or null to select the standard binary operation names.</param>
    internal static void Write(string directory, string payload, string? methodName = null)
    {
        var assembly = BenchmarkImage.Load(directory);
        var serializer = assembly.GetType("LuminPack.LuminPackSerializer", throwOnError: true)!;
        foreach (var method in serializer.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => methodName is null
                ? method.Name is "Serialize" or "SerializeUnion" or "Deserialize" or "DeserializeUnion"
                : method.Name == methodName)
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType.ToString().Contains(payload, StringComparison.Ordinal)))
            .OrderBy(method => method.MetadataToken))
            WriteMethod(method);
    }

    /// <summary>Decodes one method while resolving member tokens in its generic context.</summary>
    /// <param name="method">The selected method.</param>
    private static void WriteMethod(MethodInfo method)
    {
        if (method.GetMethodBody() is not { } body) return;
        byte[] il = body.GetILAsByteArray()!;
        Console.WriteLine($"\n{method.DeclaringType}::{method} IL={il.Length}, locals={body.LocalVariables.Count}, EH={body.ExceptionHandlingClauses.Count}, initlocals={body.InitLocals}, flags={method.MethodImplementationFlags}");
        using var reader = new BinaryReader(new MemoryStream(il));
        while (reader.BaseStream.Position < il.Length)
        {
            int position = (int)reader.BaseStream.Position;
            ushort code = reader.ReadByte();
            if (code == 0xfe) code = (ushort)(0xfe00 | reader.ReadByte());
            var opcode = Opcodes[code];
            string operand = ReadOperand(reader, opcode.OperandType, method);
            Console.WriteLine($"IL_{position:X4}: {opcode.Name} {operand}");
        }
    }

    /// <summary>Reads an operand using ECMA-335 widths and the selected method's token scope.</summary>
    /// <param name="reader">The IL byte stream.</param>
    /// <param name="kind">The opcode's operand encoding.</param>
    /// <param name="method">The owner used to resolve metadata tokens.</param>
    /// <returns>A readable operand or resolved branch target.</returns>
    private static string ReadOperand(BinaryReader reader, OperandType kind, MethodInfo method)
    {
        switch (kind)
        {
            case OperandType.InlineNone: return "";
            case OperandType.ShortInlineI: return reader.ReadSByte().ToString();
            case OperandType.ShortInlineVar: return reader.ReadByte().ToString();
            case OperandType.InlineVar: return reader.ReadUInt16().ToString();
            case OperandType.InlineI: return reader.ReadInt32().ToString();
            case OperandType.InlineI8: return reader.ReadInt64().ToString();
            case OperandType.ShortInlineR: return reader.ReadSingle().ToString("R");
            case OperandType.InlineR: return reader.ReadDouble().ToString("R");
            case OperandType.ShortInlineBrTarget:
                int shortDelta = reader.ReadSByte();
                return $"IL_{reader.BaseStream.Position + shortDelta:X4}";
            case OperandType.InlineBrTarget:
                int delta = reader.ReadInt32();
                return $"IL_{reader.BaseStream.Position + delta:X4}";
            case OperandType.InlineSwitch:
                int count = reader.ReadInt32();
                long end = reader.BaseStream.Position + count * 4;
                return string.Join(", ", Enumerable.Range(0, count).Select(_ => $"IL_{end + reader.ReadInt32():X4}"));
            default:
                int token = reader.ReadInt32();
                if (kind == OperandType.InlineString) return '"' + method.Module.ResolveString(token) + '"';
                if (kind == OperandType.InlineSig) return $"signature 0x{token:X8}";
                return method.Module.ResolveMember(token, method.DeclaringType!.GetGenericArguments(), method.GetGenericArguments())?.ToString() ?? $"0x{token:X8}";
        }
    }
}

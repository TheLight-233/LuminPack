using System.Reflection;
using System.Reflection.Emit;
using LuminPack;

internal static class EntryAudit
{
    /// <summary>Checks fixture IL for calls to concrete LuminPack serializer entries.</summary>
    /// <returns>The number of verified concrete calls.</returns>
    internal static int Check()
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => unchecked((ushort)opcode.Value));
        const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        int calls = 0;
        foreach (Type type in typeof(Fixtures).GetNestedTypes(BindingFlags.NonPublic).Append(typeof(Fixtures)))
        foreach (MethodInfo method in type.GetMethods(methods))
        {
            if (method.ContainsGenericParameters || method.GetMethodBody()?.GetILAsByteArray() is not byte[] il)
                continue;

            for (int position = 0; position < il.Length;)
            {
                ushort code = il[position++];
                if (code == 0xfe)
                    code = (ushort)(0xfe00 | il[position++]);

                var operand = opcodes[code].OperandType;
                if (operand == OperandType.InlineMethod)
                {
                    var target = method.Module.ResolveMethod(BitConverter.ToInt32(il, position));
                    if (target?.DeclaringType?.FullName == typeof(LuminPackSerializer).FullName)
                    {
                        if (target.IsGenericMethod)
                            throw new InvalidOperationException($"Fixture calls a generic LuminPack entry: {method.Name} -> {target}");
                        calls++;
                    }
                }

                position += operand switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, position),
                    _ => 4
                };
            }
        }

        if (calls == 0)
            throw new InvalidOperationException("No concrete fixture calls found.");
        return calls;
    }
}

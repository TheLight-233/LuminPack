using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LuminPack.Code.Core;

/// <summary>Tracks directly assigned JSON fields until missing members receive their original defaults.</summary>
internal sealed class JsonFieldPresence
{
    private readonly Dictionary<LuminDataField, int> _positions = new();
    private readonly string[] _locals;
    private readonly bool _packed;

    /// <summary>Allocates bounded 32-bit flags without tracking members already staged in default-initialized locals.</summary>
    /// <param name="data">The original object contract and enclosing generic identifiers.</param>
    internal JsonFieldPresence(LuminDataInfo data)
    {
        var fields = data.fields.Where(static field => !field.IsPrivate && !field.isProperty).ToArray();
        _packed = fields.Length >= 4;
        _locals = new string[_packed ? (fields.Length + 31) / 32 : fields.Length];
        var identifiers = new HashSet<string>(data.GenericParameters.Select(name => name.TrimStart('@')), StringComparer.Ordinal);
        for (var owner = data.TypeSymbol; owner != null; owner = owner.ContainingType)
            foreach (var parameter in owner.TypeParameters)
                identifiers.Add(parameter.Name);
        foreach (var field in data.fields)
        {
            identifiers.Add(field.Name);
            identifiers.Add(field.Name + "Temp");
        }
        for (int i = 0; i < fields.Length; i++)
            _positions.Add(fields[i], i);
        for (int i = 0; i < _locals.Length; i++)
        {
            string name = _packed ? "seenFields" + i : fields[i].Name + "Seen";
            while (!identifiers.Add(name))
                name += "_";
            _locals[i] = name;
        }
    }

    /// <summary>Initializes presence storage once per object while retaining scalar flags for tiny contracts.</summary>
    /// <param name="builder">The generated operation body.</param>
    /// <param name="indent">The declaration indentation.</param>
    internal void AppendDeclarations(StringBuilder builder, string indent)
    {
        foreach (string local in _locals)
            builder.AppendLine(indent + (_packed ? "uint " : "bool ") + local + (_packed ? " = 0;" : " = false;"));
    }

    /// <summary>Marks a recognized direct field at its original position before reading its value.</summary>
    /// <param name="builder">The generated operation body.</param>
    /// <param name="field">The recognized member, which may instead use staged storage.</param>
    /// <param name="indent">The assignment indentation.</param>
    internal void AppendMark(StringBuilder builder, LuminDataField field, string indent)
    {
        string statement = Mark(field);
        if (statement != null)
            builder.AppendLine(indent + statement);
    }

    /// <summary>Spells the assignment that marks a directly writable field as present.</summary>
    /// <param name="field">The recognized member.</param>
    /// <returns>The existing mark statement, or null for a staged field.</returns>
    internal string Mark(LuminDataField field) => !_positions.TryGetValue(field, out int position) ? null : _packed
        ? _locals[position / 32] + " |= " + Mask(position) + ";"
        : _locals[position] + " = true;";

    /// <summary>Builds the original post-read missing-field test without accessing user storage.</summary>
    /// <param name="field">The directly assigned member.</param>
    /// <returns>The scalar or packed presence test.</returns>
    internal string MissingCondition(LuminDataField field)
    {
        int position = _positions[field];
        return _packed ? "(" + _locals[position / 32] + " & " + Mask(position) + ") == 0" : "!" + _locals[position];
    }

    /// <summary>Emits one unsigned bit, including the high bit, without target-sized or 64-bit arithmetic.</summary>
    /// <param name="position">The ordinal among directly assigned fields.</param>
    /// <returns>A compile-time unsigned 32-bit literal.</returns>
    private static string Mask(int position) => (1u << (position % 32)).ToString(CultureInfo.InvariantCulture) + "u";
}

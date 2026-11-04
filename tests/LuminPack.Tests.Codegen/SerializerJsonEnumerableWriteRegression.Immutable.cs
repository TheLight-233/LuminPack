/// <summary>Checks linear immutable sequences and Frozen traversal against independent scalar writes.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    /// <summary>Builds concrete immutable calls and binary Frozen scalar-oracle comparisons.</summary>
    /// <returns>The generated statements inside the existing profile and boundary-size matrix.</returns>
    private static string BuildImmutableCases()
    {
        var calls = new System.Text.StringBuilder();
        foreach (string element in new[] { "int", "long", "string" })
        {
            string items = element == "int" ? "ints" : element == "long" ? "longs" : "texts";
            foreach (string family in new[] { "ImmutableArray", "ImmutableList", "ImmutableQueue", "ImmutableStack", "ImmutableHashSet", "ImmutableSortedSet",
                         "IImmutableList", "IImmutableSet", "IImmutableQueue", "IImmutableStack", "FrozenSet" })
            {
                string factory = family switch
                {
                    "FrozenSet" => items + ".ToFrozenSet()",
                    "IImmutableList" => "ImmutableArray.CreateRange(" + items + ")",
                    "IImmutableSet" => "ImmutableHashSet.CreateRange(" + items + ")",
                    "IImmutableQueue" => "ImmutableQueue.CreateRange(" + items + ")",
                    "IImmutableStack" => "ImmutableStack.CreateRange(" + items + ")",
                    _ => family + ".CreateRange(" + items + ")"
                };
                calls.AppendLine("{ " + family + "<" + element + "> value = " + factory + ";");
                calls.AppendLine("string expected = Expected(value); buffer.ResetCore(); LuminPackSerializer.SerializeJson<TProfile>(value, buffer); EqualText<TProfile>(buffer, expected);");
                if (family == "FrozenSet")
                    calls.AppendLine("""
                        buffer.ResetCore(); LuminPackSerializer.Serialize<TProfile>(value, buffer); byte[] actual = buffer.GetSpan().ToArray();
                        buffer.ResetCore(); var scalar = new LuminPackWriter(buffer); scalar.EnsureAdditionalCapacity(4); scalar.Write(value.Count);
                        foreach (var item in value) { scalar.CheckBuffer(); LuminPackSerializer.Serialize<TProfile>(ref scalar, in item); }
                        scalar.CheckBuffer(); if (!actual.AsSpan().SequenceEqual(scalar.GetSpan())) throw new Exception("Frozen set scalar bytes differ.");
                        """);
                calls.AppendLine("}");
            }
            calls.AppendLine("{ var value = Enumerable.Range(0," + items + ".Length).ToFrozenDictionary(i => i, i => " + items + "[i]);");
            calls.AppendLine("""
                string expected = ("[" + string.Join(",", value.Select(pair => "[" + pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + JsonSerializer.Serialize(pair.Value,jsonOptions) + "]")) + "]").Replace("\\uD83D\\uDE00", "\uD83D\uDE00");
                buffer.ResetCore(); LuminPackSerializer.SerializeJson<TProfile>(value, buffer); EqualText<TProfile>(buffer, expected);
                buffer.ResetCore(); LuminPackSerializer.Serialize<TProfile>(value, buffer); byte[] actual = buffer.GetSpan().ToArray();
                buffer.ResetCore(); var scalar = new LuminPackWriter(buffer); scalar.EnsureAdditionalCapacity(4); scalar.Write(value.Count);
                foreach (var item in value)
                {
                    scalar.CheckBuffer(); var key = item.Key; var entryValue = item.Value;
                    LuminPackSerializer.Serialize<TProfile>(ref scalar,in key); LuminPackSerializer.Serialize<TProfile>(ref scalar,in entryValue);
                }
                scalar.CheckBuffer(); if (!actual.AsSpan().SequenceEqual(scalar.GetSpan())) throw new Exception("Frozen dictionary scalar bytes differ.");
                }
                """);
        }
        return calls.ToString();
    }
}

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

internal static class Canonical
{
    /// <summary>Converts a fixture value to its canonical comparison string.</summary>
    /// <param name="value">The value to process.</param>
    /// <returns>The resulting text.</returns>
    internal static string Of(object? value) => JsonSerializer.Serialize(Tree(value));

    /// <summary>Normalizes a fixture value as a tree for deterministic comparison.</summary>
    /// <param name="value">The value to process.</param>
    /// <returns>The resulting <c>object</c>, or null when unavailable.</returns>
    private static object? Tree(object? value)
    {
        if (value is null)
            return null;

        Type type = value.GetType();
        if (value is DateTime date)
            return date.ToBinary();
        if (value is DateTimeOffset offset)
            return offset.ToString("O", CultureInfo.InvariantCulture);
        if (value is TimeZoneInfo zone)
            return zone.ToSerializedString();
        if (value is Type reflected)
            return reflected.AssemblyQualifiedName;
        if (value is CultureInfo culture)
            return culture.Name;
        if (type.IsPrimitive || type.IsEnum || value is string or decimal or Half or Int128 or UInt128
            or System.Numerics.BigInteger or Rune or Guid or TimeSpan or DateOnly or TimeOnly or Uri or Version or StringBuilder)
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PriorityQueue<,>))
        {
            var entries = (IEnumerable)type.GetProperty("UnorderedItems")!.GetValue(value)!;
            return entries.Cast<object>().Select(Of).Order(StringComparer.Ordinal).ToArray();
        }
        if (value is IEnumerable sequence)
        {
            var items = sequence.Cast<object?>().Select(Tree);
            if (type.Name.Contains("Set", StringComparison.Ordinal) || type.Name.Contains("Dictionary", StringComparison.Ordinal)
                || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ConcurrentBag<>))
                return items.Select(item => JsonSerializer.Serialize(item)).Order(StringComparer.Ordinal).ToArray();
            return items.ToArray();
        }

        var members = new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["$type"] = type.FullName };
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            members[field.Name] = Tree(field.GetValue(value));
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.CanRead && property.GetIndexParameters().Length == 0)
                members[property.Name] = Tree(property.GetValue(value));
        return members;
    }
}

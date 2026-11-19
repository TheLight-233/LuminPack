using System.Collections;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using LuminPack.NinoBenchmark.Workloads;

namespace LuminPack.NinoBenchmark;

internal static class FixtureVerification
{
    /// <summary>Checks the benchmark pairs, including the retained read destinations.</summary>
    /// <param name="benchmarks">The benchmark fixture classes.</param>
    /// <returns>Zero when every pair succeeds.</returns>
    internal static int Run(IEnumerable<Type> benchmarks)
    {
        int count = 0;
        foreach (Type type in benchmarks)
        {
            object instance = Activator.CreateInstance(type)!;
            type.GetMethod("Setup")!.Invoke(instance, null);
            try
            {
                var categories = type.GetMethods().Where(method => method.IsDefined(typeof(BenchmarkAttribute)))
                    .GroupBy(method => method.GetCustomAttribute<BenchmarkCategoryAttribute>()!.Categories.Single());
                foreach (var category in categories)
                {
                    MethodInfo nino = category.Single(method => method.GetCustomAttribute<BenchmarkAttribute>()!.Baseline);
                    MethodInfo luminPack = category.Single(method => method != nino);
                    object? previousNino = null;
                    object? previousLuminPack = null;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        object? expected = nino.Invoke(instance, null);
                        object? actual = luminPack.Invoke(instance, null);
                        bool valid = expected is int ninoSize && actual is int luminPackSize
                            ? ninoSize == PayloadSize(type, nino.Name) && luminPackSize == PayloadSize(type, luminPack.Name)
                            : expected != null && Same(expected, actual,
                                new Dictionary<object, object>(ReferenceEqualityComparer.Instance),
                                new HashSet<object>(ReferenceEqualityComparer.Instance));
                        if (!valid)
                            throw new InvalidOperationException($"Fixture verification failed for {type.Name}.{category.Key} on pass {pass + 1}.");
                        if (pass != 0 && expected is StateSnapshot or IdleSaveV2 &&
                            (!ReferenceEquals(expected, previousNino) || !ReferenceEquals(actual, previousLuminPack)))
                            throw new InvalidOperationException($"A retained destination was replaced in {type.Name}.{category.Key}.");

                        previousNino = expected;
                        previousLuminPack = actual;
                    }
                    count++;
                }
            }
            finally
            {
                type.GetMethod("Cleanup")!.Invoke(instance, null);
            }
        }

        if (count != 40)
            throw new InvalidOperationException($"Expected 40 benchmark pairs, found {count}.");
        Console.WriteLine($"Verified {count} Nino/LuminPack benchmark pairs, twice each.");
        return 0;
    }

    /// <summary>Gets the stored length for a timed write.</summary>
    /// <param name="type">The benchmark fixture class.</param>
    /// <param name="method">The benchmark write method.</param>
    /// <returns>The serializer's encoded fixture width.</returns>
    private static int PayloadSize(Type type, string method)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        string field = method.Replace("Serialize", "", StringComparison.Ordinal);
        return ((byte[])(type.GetField(field + "Payload", flags) ?? type.GetField(field + "Bytes", flags))!.GetValue(null)!).Length;
    }

    /// <summary>Compares the fixture graph by field and collection contents.</summary>
    /// <param name="expected">The baseline value.</param>
    /// <param name="actual">The comparison value.</param>
    /// <param name="references">The correspondence between restored reference-tracked objects.</param>
    /// <param name="targets">The tracked objects already matched to a baseline object.</param>
    /// <returns>Whether both serializers restored the same values.</returns>
    private static bool Same(object? expected, object? actual,
        Dictionary<object, object> references, HashSet<object> targets)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected is null || actual is null || expected.GetType() != actual.GetType()) return false;
        Type type = expected.GetType();
        if (expected is DateTime timestamp && actual is DateTime otherTimestamp)
            return timestamp.Ticks == otherTimestamp.Ticks && timestamp.Kind == otherTimestamp.Kind;
        if (expected is string or Guid or decimal || type.IsPrimitive || type.IsEnum)
            return expected.Equals(actual);
        if (expected is Alliance or CardDef)
        {
            if (references.TryGetValue(expected, out object? target))
                return ReferenceEquals(target, actual);
            if (!targets.Add(actual))
                return false;
            references.Add(expected, actual);
        }
        if (expected is IDictionary keys && actual is IDictionary values)
            return keys.Count == values.Count && keys.Keys.Cast<object>()
                .All(key => values.Contains(key) && Same(keys[key], values[key], references, targets));
        if (expected is IEnumerable left && actual is IEnumerable right)
        {
            object?[] leftItems = left.Cast<object?>().ToArray();
            object?[] rightItems = right.Cast<object?>().ToArray();
            return leftItems.Length == rightItems.Length &&
                leftItems.Zip(rightItems).All(pair => Same(pair.First, pair.Second, references, targets));
        }
        return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .All(field => Same(field.GetValue(expected), field.GetValue(actual), references, targets));
    }
}

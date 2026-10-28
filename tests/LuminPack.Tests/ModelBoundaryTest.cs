using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // 1. init-only auto-property (plain class)
    /// <summary>Provides init only model functionality.</summary>
    [LuminPackable]
    public partial class InitOnlyModel
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; init; } = "";
        /// <summary>Gets or sets the count.</summary>
        public int Count { get; init; }
    }

    // 2. get-only auto-property with a matching constructor
    /// <summary>Provides get only with ctor functionality.</summary>
    [LuminPackable]
    public partial class GetOnlyWithCtor
    {
        /// <summary>Gets the name.</summary>
        public string Name { get; }
        /// <summary>Initializes a new instance of <c>GetOnlyWithCtor</c>.</summary>
        /// <param name="name">The name used by this operation.</param>
        public GetOnlyWithCtor(string name) { Name = name; }
    }

    // 3. get-only auto-property, no constructor
    /// <summary>Provides get only no ctor functionality.</summary>
    [LuminPackable]
    public partial class GetOnlyNoCtor
    {
        /// <summary>Gets the name.</summary>
        public string Name { get; }
    }

    // 4. C#12 primary constructor, auto-property initialized from the parameter
    /// <summary>Provides primary ctor auto functionality.</summary>
    [LuminPackable]
    public partial class PrimaryCtorAuto(string name)
    {
        /// <summary>Gets the name.</summary>
        public string Name { get; } = name;
    }

    // 5. C#12 primary constructor, computed property from the parameter (candidate for data loss).
    //    LuminPack rejects this at compile time with diagnostic LuminPack026
    //    ("constructor parameter does not match a serializable member"), so it is intentionally
    //    not a serializable type and is not part of the round-trip tests below.

    // 6. positional record
    /// <summary>Provides positional record functionality.</summary>
    [LuminPackable]
    public partial record PositionalRecord(int Id, string Name);

    // 7. record with an extra init property
    /// <summary>Provides mixed record functionality.</summary>
    [LuminPackable]
    public partial record MixedRecord(int Id)
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; init; } = "";
    }

    // 8. positional record struct
    /// <summary>Provides pos record struct functionality.</summary>
    [LuminPackable]
    public partial record struct PosRecordStruct(int Id, string Name);

    // 9. readonly positional record struct
    /// <summary>Provides readonly record struct functionality.</summary>
    [LuminPackable]
    public readonly partial record struct ReadonlyRecordStruct(int Id, string Name);

    /// <summary>Provides model boundary test functionality.</summary>
    public static class ModelBoundaryTest
    {
        /// <summary>Verifies round trip.</summary>
        /// <param name="value">The value consumed or produced by this operation.</param>
        /// <param name="name">The name used by this operation.</param>
        /// <param name="results">The results used by this operation.</param>
        /// <param name="same">The same used by this operation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        private static void RoundTrip<T>(T value, string name, List<string> results, Func<T, T, bool> same)
        {
            try
            {
                var bytes = LuminPackSerializer.Serialize(value);
                var back = LuminPackSerializer.Deserialize<T>(bytes);
                results.Add(same(value, back)
                    ? $"✓ {name} - PASSED"
                    : $"✗ {name} - FAILED (round-trip mismatch)");
            }
            catch (Exception ex)
            {
                results.Add($"✗ {name} - ERROR: {ex.Message}");
            }
        }

        /// <summary>Verifies ref same.</summary>
        /// <param name="a">The a used by this operation.</param>
        /// <param name="b">The b used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private static bool RefSame(string? a, string? b) => a == b;

        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void Run(List<string> results)
        {
            RoundTrip(new InitOnlyModel { Name = "init", Count = 42 }, "ModelBoundary InitOnlyModel", results,
                (a, b) => RefSame(a.Name, b.Name) && a.Count == b.Count);

            RoundTrip(new GetOnlyWithCtor("ctor-set"), "ModelBoundary GetOnlyWithCtor", results,
                (a, b) => RefSame(a.Name, b.Name));

            RoundTrip(new GetOnlyNoCtor(), "ModelBoundary GetOnlyNoCtor", results,
                (a, b) => RefSame(a.Name, b.Name));

            RoundTrip(new PrimaryCtorAuto("primary"), "ModelBoundary PrimaryCtorAuto", results,
                (a, b) => RefSame(a.Name, b.Name));

            RoundTrip(new PositionalRecord(7, "rec"), "ModelBoundary PositionalRecord", results,
                (a, b) => a.Id == b.Id && RefSame(a.Name, b.Name));

            RoundTrip(new MixedRecord(9) { Name = "mixed" }, "ModelBoundary MixedRecord", results,
                (a, b) => a.Id == b.Id && RefSame(a.Name, b.Name));

            RoundTrip(new PosRecordStruct(11, "rstruct"), "ModelBoundary PosRecordStruct", results,
                (a, b) => a.Id == b.Id && RefSame(a.Name, b.Name));

            RoundTrip(new ReadonlyRecordStruct(13, "ro"), "ModelBoundary ReadonlyRecordStruct", results,
                (a, b) => a.Id == b.Id && RefSame(a.Name, b.Name));
        }
    }
}

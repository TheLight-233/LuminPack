using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    // 1. init-only auto-property (plain class)
    [LuminPackable]
    public partial class InitOnlyModel
    {
        public string Name { get; init; } = "";
        public int Count { get; init; }
    }

    // 2. get-only auto-property with a matching constructor
    [LuminPackable]
    public partial class GetOnlyWithCtor
    {
        public string Name { get; }
        public GetOnlyWithCtor(string name) { Name = name; }
    }

    // 3. get-only auto-property, no constructor
    [LuminPackable]
    public partial class GetOnlyNoCtor
    {
        public string Name { get; }
    }

    // 4. C#12 primary constructor, auto-property initialized from the parameter
    [LuminPackable]
    public partial class PrimaryCtorAuto(string name)
    {
        public string Name { get; } = name;
    }

    // 5. C#12 primary constructor, computed property from the parameter (candidate for data loss).
    //    LuminPack rejects this at compile time with diagnostic LuminPack026
    //    ("constructor parameter does not match a serializable member"), so it is intentionally
    //    not a serializable type and is not part of the round-trip tests below.

    // 6. positional record
    [LuminPackable]
    public partial record PositionalRecord(int Id, string Name);

    // 7. record with an extra init property
    [LuminPackable]
    public partial record MixedRecord(int Id)
    {
        public string Name { get; init; } = "";
    }

    // 8. positional record struct
    [LuminPackable]
    public partial record struct PosRecordStruct(int Id, string Name);

    // 9. readonly positional record struct
    [LuminPackable]
    public readonly partial record struct ReadonlyRecordStruct(int Id, string Name);

    public static class ModelBoundaryTest
    {
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

        private static bool RefSame(string? a, string? b) => a == b;

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
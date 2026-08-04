using LuminPack;
using LuminPack.Core;

namespace LuminPackUnitTest;

internal static class ParserRegistrationRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(WellKnownParserSurvivesCacheBootstrap), WellKnownParserSurvivesCacheBootstrap);
        RunCase(results, nameof(TryRegisterParserPublishesCreatedParser), TryRegisterParserPublishesCreatedParser);
        RunCase(results, nameof(EightArgumentTupleUsesMatchingParserArity), EightArgumentTupleUsesMatchingParserArity);
    }

    private static void WellKnownParserSurvivesCacheBootstrap()
    {
        var value = "cache-bootstrap-亮度-😀";
        Span<byte> buffer = stackalloc byte[128];
        var writer = new LuminPackWriter(ref buffer);
        LuminPackLocalExtension.WriteValue<string>(ref writer, in value);

        var payload = buffer.Slice(0, writer.CurrentIndex);
        var reader = new LuminPackReader(ref payload);
        string result = null!;
        LuminPackLocalExtension.ReadValue(ref reader, ref result);

        Assert(result == value,
            "The first generic Cache<string> initialization overwrote the well-known StringParser.");
    }

    private static void TryRegisterParserPublishesCreatedParser()
    {
        Assert(!LuminPackParseProvider.IsRegistered<RegistrationProbe>(),
            "Registration probe was unexpectedly initialized before the test.");
        Assert(LuminPackParseProvider.TryRegisterParser<RegistrationProbe>(),
            "TryRegisterParser returned false for a supported unmanaged type.");
        Assert(LuminPackParseProvider.IsRegistered<RegistrationProbe>(),
            "TryRegisterParser did not publish the parser into the generic cache.");

        var value = new RegistrationProbe { A = 42, B = 1.25 };
        var roundTrip = LuminPackSerializer.Deserialize<RegistrationProbe>(LuminPackSerializer.Serialize(value));
        Assert(roundTrip.A == value.A && roundTrip.B == value.B,
            "The parser published by TryRegisterParser could not round-trip its value.");
    }

    private static void EightArgumentTupleUsesMatchingParserArity()
    {
        var value = new Tuple<int, int, int, int, int, int, int, Tuple<int>>(
            1, 2, 3, 4, 5, 6, 7, Tuple.Create(8));
        var bytes = LuminPackSerializer.Serialize(value);
        var roundTrip = LuminPackSerializer.Deserialize<Tuple<int, int, int, int, int, int, int, Tuple<int>>>(bytes);

        Assert(roundTrip is not null &&
               roundTrip.Item1 == 1 && roundTrip.Item7 == 7 && roundTrip.Rest.Item1 == 8,
            "The eight-argument Tuple parser mapping used the wrong generic arity.");
    }

    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private struct RegistrationProbe
    {
        public int A;
        public double B;
    }
}

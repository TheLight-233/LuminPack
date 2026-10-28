using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;

namespace LuminPackUnitTest;

/// <summary>Provides dynamic union base functionality.</summary>
[LuminPackable]
[LuminPackUnion(1, typeof(StaticDynamicUnionMember))]
public abstract partial class DynamicUnionBase
{
}

/// <summary>Provides static dynamic union member functionality.</summary>
[LuminPackable]
public sealed partial class StaticDynamicUnionMember : DynamicUnionBase
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides i fast interface union functionality.</summary>
[LuminPackable]
[LuminPackUnion(7, typeof(FastInterfaceUnionMember))]
public partial interface IFastInterfaceUnion
{
}

/// <summary>Provides fast interface union member functionality.</summary>
[LuminPackable]
public sealed partial class FastInterfaceUnionMember : IFastInterfaceUnion
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides registered interface union member functionality.</summary>
public sealed class RegisteredInterfaceUnionMember : IFastInterfaceUnion
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides i wide union functionality.</summary>
[LuminPackable]
[LuminPackWideTag]
[LuminPackUnion(60_000, typeof(WideUnionMember))]
public partial interface IWideUnion
{
}

/// <summary>Provides wide union member functionality.</summary>
[LuminPackable]
public sealed partial class WideUnionMember : IWideUnion
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides i generic fast union functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public partial interface IGenericFastUnion<T>
{
}

/// <summary>Provides generic fast union member functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public sealed partial class GenericFastUnionMember<T> : IGenericFastUnion<T>
{
    /// <summary>Stores the value.</summary>
    public T Value = default!;
}

/// <summary>Provides i mixed generic union functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
[LuminPackUnion(11, typeof(MixedGenericUnionMember<int>))]
[LuminPackUnion(12, typeof(MixedGenericUnionMember<string>))]
public partial interface IMixedGenericUnion<T>
{
}

/// <summary>Provides mixed generic union member functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public sealed partial class MixedGenericUnionMember<T> : IMixedGenericUnion<T>
{
    /// <summary>Stores the value.</summary>
    public T Value = default!;
}

/// <summary>Provides first class union root functionality.</summary>
[LuminPackable]
public abstract partial class FirstClassUnionRoot
{
}

/// <summary>Provides second class union root functionality.</summary>
[LuminPackable]
public abstract partial class SecondClassUnionRoot : FirstClassUnionRoot
{
}

/// <summary>Provides multiple class union member functionality.</summary>
[LuminPackable]
public sealed partial class MultipleClassUnionMember : SecondClassUnionRoot
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides binary union safety test functionality.</summary>
internal static class BinaryUnionSafetyTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(BoxedStructUnionRoundTripsAndSizeMatches), BoxedStructUnionRoundTripsAndSizeMatches);
        RunCase(results, nameof(ClassUnionUsesStaticTagDispatch), ClassUnionUsesStaticTagDispatch);
        RunCase(results, nameof(InterfaceUnionUsesStaticTagDispatch), InterfaceUnionUsesStaticTagDispatch);
        RunCase(results, nameof(KnownTagUsesStaticDeserializeSwitch), KnownTagUsesStaticDeserializeSwitch);
        RunCase(results, nameof(UnlistedUnionMemberIsRejected), UnlistedUnionMemberIsRejected);
        RunCase(results, nameof(RegisteredUnlistedMemberRoundTrips), RegisteredUnlistedMemberRoundTrips);
        RunCase(results, nameof(NullWideGenericAndMultipleRootsRoundTrip), NullWideGenericAndMultipleRootsRoundTrip);
        RunCase(results, nameof(ClosedGenericTagsUseConstantTimeDispatch), ClosedGenericTagsUseConstantTimeDispatch);
        RunCase(results, nameof(DynamicUnionRegistrationIsNotAvailable), DynamicUnionRegistrationIsNotAvailable);
    }

    /// <summary>Verifies class union uses static tag dispatch.</summary>
    private static void ClassUnionUsesStaticTagDispatch()
    {
        DynamicUnionBase value = new StaticDynamicUnionMember { Value = 123 };
        var payload = LuminPackSerializer.Serialize(value);
        Assert(payload[0] == 1, "The class union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The class union static Sizeof path disagrees with Serialize.");

        var result = LuminPackSerializer.Deserialize<DynamicUnionBase>(payload);
        Assert(result is StaticDynamicUnionMember { Value: 123 },
            "The class union fast path did not round-trip through the static tag switch.");
    }

    /// <summary>Verifies interface union uses static tag dispatch.</summary>
    private static void InterfaceUnionUsesStaticTagDispatch()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 456 };
        var payload = LuminPackSerializer.Serialize(value);
        var json = LuminPackSerializer.SerializeJson(value);
        Assert(payload[0] == 7, "The interface union fast path wrote the wrong constant tag.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            "The interface union static Sizeof path disagrees with Serialize.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 456 },
            "The interface union binary fast path did not round-trip.");
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is FastInterfaceUnionMember { Value: 456 },
            "The interface union JSON fast path did not round-trip.");
    }

    /// <summary>Verifies known tag uses static deserialize switch.</summary>
    private static void KnownTagUsesStaticDeserializeSwitch()
    {
        IFastInterfaceUnion value = new FastInterfaceUnionMember { Value = 789 };
        var payload = LuminPackSerializer.Serialize(value);

        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is FastInterfaceUnionMember { Value: 789 },
            "A known local tag did not deserialize through the generated switch.");
    }

    /// <summary>Verifies unlisted union member is rejected.</summary>
    private static void UnlistedUnionMemberIsRejected()
    {
        IFastInterfaceUnion value = new RegisteredInterfaceUnionMember { Value = 321 };
        AssertThrows(() => LuminPackSerializer.Serialize(value),
            "An unlisted union member was accepted without a generated tag.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(value),
            "An unlisted JSON union member was accepted without a generated tag.");
    }

    /// <summary>Verifies null wide generic and multiple roots round trip.</summary>
    private static void NullWideGenericAndMultipleRootsRoundTrip()
    {
        IFastInterfaceUnion? nullValue = null;
        var nullPayload = LuminPackSerializer.Serialize(nullValue);
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(nullPayload) is null,
            "A null interface union did not round-trip.");

        IWideUnion wide = new WideUnionMember { Value = 42 };
        var widePayload = LuminPackSerializer.Serialize(wide);
        Assert(widePayload[0] == LuminPack.Code.LuminPackCode.WideTag,
            "The wide-tag virtual path did not preserve the wide union header.");
        Assert(LuminPackSerializer.Deserialize<IWideUnion>(widePayload) is WideUnionMember { Value: 42 },
            "A wide-tag union did not round-trip.");

        IGenericFastUnion<int> generic = new GenericFastUnionMember<int> { Value = 73 };
        var genericPayload = LuminPackSerializer.Serialize(generic);
        Assert(LuminPackSerializer.Deserialize<IGenericFastUnion<int>>(genericPayload) is GenericFastUnionMember<int> { Value: 73 },
            "A generic interface union did not round-trip.");

        var member = new MultipleClassUnionMember { Value = 88 };
        FirstClassUnionRoot first = member;
        SecondClassUnionRoot second = member;
        var firstPayload = LuminPackSerializer.Serialize(first);
        var secondPayload = LuminPackSerializer.Serialize(second);
        Assert(LuminPackSerializer.Deserialize<FirstClassUnionRoot>(firstPayload) is MultipleClassUnionMember { Value: 88 },
            "The first independent class-union virtual slot failed.");
        Assert(LuminPackSerializer.Deserialize<SecondClassUnionRoot>(secondPayload) is MultipleClassUnionMember { Value: 88 },
            "The second independent class-union virtual slot failed.");
    }

    /// <summary>Verifies registered unlisted member round trips.</summary>
    private static void RegisteredUnlistedMemberRoundTrips()
    {
        unsafe
        {
            IFastInterfaceUnion.Register<RegisteredInterfaceUnionMember>(100,
                &WriteRegisteredMember, &ReadRegisteredMember, &WriteJsonRegisteredMember, &ReadJsonRegisteredMember);
        }

        Assert(IFastInterfaceUnion.TryGetRegisteredFormatter(100, out var probeEntry) && probeEntry.ReadValue != 0,
            "probe: registered formatter not found by tag after Register.");

        IFastInterfaceUnion value = new RegisteredInterfaceUnionMember { Value = 321 };
        var payload = LuminPackSerializer.Serialize(value);
        Assert(payload[0] == 100, "The registered union member wrote the wrong wire tag.");
        Assert(LuminPackSerializer.Deserialize<IFastInterfaceUnion>(payload) is RegisteredInterfaceUnionMember { Value: 321 },
            "The registered union member did not round-trip.");
        var json = LuminPackSerializer.SerializeJson(value);
        Assert(LuminPackSerializer.DeserializeJson<IFastInterfaceUnion>(json) is RegisteredInterfaceUnionMember { Value: 321 },
            "The registered union member did not round-trip through JSON.");
    }

    /// <summary>Writes registered member to the current output.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void WriteRegisteredMember(ref LuminPackWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteUnionHeader(100);
        writer.Write(LuminPackMarshal.As<IFastInterfaceUnion, RegisteredInterfaceUnionMember>(ref value).Value);
    }

    /// <summary>Reads registered member from the current input.</summary>
    /// <param name="reader">The reader supplying the input data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void ReadRegisteredMember(ref LuminPackReader reader, ref IFastInterfaceUnion value)
    {
        var v = new RegisteredInterfaceUnionMember();
        reader.Read(ref v.Value);
        value = LuminPackMarshal.As<RegisteredInterfaceUnionMember, IFastInterfaceUnion>(ref v);
    }

    /// <summary>Writes json registered member to the current output.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void WriteJsonRegisteredMember(ref LuminPackJsonWriter writer, ref IFastInterfaceUnion value)
    {
        writer.WriteObjectStart();
        if (default(LuminPack.Option.LuminPackDefaultOptions).StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
            writer.WritePropertyName(LuminPackConstUtf8.TypeU8);
        else
            writer.WritePropertyName(LuminPackConstUtf8.TypeU16);
        writer.Write(100);
        if (default(LuminPack.Option.LuminPackDefaultOptions).StringEncoding == LuminPack.Option.LuminPackStringEncoding.UTF8)
            writer.WritePropertyName(LuminPackConstUtf8.ValueU8);
        else
            writer.WritePropertyName(LuminPackConstUtf8.ValueU16);
        writer.Write(LuminPackMarshal.As<IFastInterfaceUnion, RegisteredInterfaceUnionMember>(ref value).Value);
        writer.WriteObjectEnd();
    }

    /// <summary>Reads json registered member from the current input.</summary>
    /// <param name="reader">The reader supplying the input data.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void ReadJsonRegisteredMember(ref LuminPackJsonReader reader, ref IFastInterfaceUnion value)
    {
        var v = new RegisteredInterfaceUnionMember();
        reader.Read(ref v.Value);
        value = LuminPackMarshal.As<RegisteredInterfaceUnionMember, IFastInterfaceUnion>(ref v);
    }

    /// <summary>Verifies closed generic tags use constant time dispatch.</summary>
    private static void ClosedGenericTagsUseConstantTimeDispatch()
    {
        IMixedGenericUnion<int> closed = new MixedGenericUnionMember<int> { Value = 91 };
        IMixedGenericUnion<string> second = new MixedGenericUnionMember<string> { Value = "second" };

        var closedPayload = LuminPackSerializer.Serialize(closed);
        var secondPayload = LuminPackSerializer.Serialize(second);

        Assert(closedPayload[0] == 11,
            "The first explicit closed-generic union member wrote the wrong wire tag.");
        Assert(secondPayload[0] == 12,
            "The second explicit closed-generic union member wrote the wrong wire tag.");
        Assert(LuminPackSerializer.Sizeof(closed) == closedPayload.Length,
            "The first closed-generic union member size did not match its payload.");
        Assert(LuminPackSerializer.Sizeof(second) == secondPayload.Length,
            "The second closed-generic union member size did not match its payload.");
        Assert(LuminPackSerializer.Deserialize<IMixedGenericUnion<int>>(closedPayload) is
                MixedGenericUnionMember<int> { Value: 91 },
            "The first explicit closed-generic union member did not round-trip.");
        Assert(LuminPackSerializer.Deserialize<IMixedGenericUnion<string>>(secondPayload) is
                MixedGenericUnionMember<string> { Value: "second" },
            "The second explicit closed-generic union member did not round-trip.");
    }

    /// <summary>Verifies boxed struct union round trips and size matches.</summary>
    private static void BoxedStructUnionRoundTripsAndSizeMatches()
    {
        ISerializable value = new Struct1
        {
            A = 123456789,
            B = new DateTime(2026, 7, 28, 12, 34, 56, DateTimeKind.Utc),
            C = Guid.Parse("8bd49956-c49f-4ba2-a880-c7527fa3db90")
        };

        var rawValue = (Struct1)value;
        var rawSize = LuminPackSerializer.Sizeof(rawValue);
        var rawPayload = LuminPackSerializer.Serialize(rawValue);
        var expectedSize = LuminPackSerializer.Sizeof(value);
        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<ISerializable>(payload);

        Assert(expectedSize == payload.Length,
            $"Boxed struct union Sizeof returned {expectedSize}, but serialization wrote {payload.Length} bytes " +
            $"(direct struct: Sizeof {rawSize}, wrote {rawPayload.Length}).");
        Assert(result is Struct1 actual &&
               actual.A == 123456789 &&
               actual.B == new DateTime(2026, 7, 28, 12, 34, 56, DateTimeKind.Utc) &&
               actual.C == Guid.Parse("8bd49956-c49f-4ba2-a880-c7527fa3db90"),
            "A boxed struct union did not survive binary round-trip.");
    }

    /// <summary>Verifies dynamic union registration is not available.</summary>
    private static void DynamicUnionRegistrationIsNotAvailable()
    {
        DynamicUnionBase value = new StaticDynamicUnionMember { Value = 1 };
        Assert(LuminPackSerializer.Deserialize<DynamicUnionBase>(LuminPackSerializer.Serialize(value)) is StaticDynamicUnionMember,
            "A statically declared union tag did not round-trip without registration.");
    }

    /// <summary>Verifies assert throws.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
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

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

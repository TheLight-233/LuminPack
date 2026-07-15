using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class TupleParser<T1> : LuminPackParser<Tuple<T1?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?>(
            reader.ReadValue<T1>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);

        reader.Read(); // consume array end

        value = new Tuple<T1?>(item1);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2> : LuminPackParser<Tuple<T1?, T2?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?>(item1, item2);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3> : LuminPackParser<Tuple<T1?, T2?, T3?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?>(item1, item2, item3);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3, T4> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?, T4?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?, T4?>(item1, item2, item3, item4);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?, T4?, T5?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?, T4?, T5?>(item1, item2, item3, item4, item5);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?>(item1, item2, item3, item4, item5, item6);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6, T7> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
        writer.WriteValue(value.Item7);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>(),
            reader.ReadValue<T7>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
        var v7 = value.Item7;
        evaluator.CalculateValue(in v7);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v7 = value.Item7;
        parser7.SerializeJson(ref writer, ref v7);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);
        reader.Read();
        T7? item7 = default;
        parser7.DeserializeJson(ref reader, ref item7);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>(item1, item2, item3, item4, item5, item6, item7);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6, T7, TRest> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value == null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
        writer.WriteValue(value.Item7);
        writer.WriteValue(value.Rest);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>(),
            reader.ReadValue<T7>(),
            reader.ReadValue<TRest>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        if (value == null)
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
        var v7 = value.Item7;
        evaluator.CalculateValue(in v7);
        var vRest = value.Rest;
        evaluator.CalculateValue(in vRest);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        var parserRest = LuminPackParseProvider.Cache<TRest>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v7 = value.Item7;
        parser7.SerializeJson(ref writer, ref v7);
        var vRest = value.Rest;
        parserRest.SerializeJson(ref writer, ref vRest);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        var parserRest = LuminPackParseProvider.Cache<TRest>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);
        reader.Read();
        T7? item7 = default;
        parser7.DeserializeJson(ref reader, ref item7);
        reader.Read();
        TRest itemRest = default;
        parserRest.DeserializeJson(ref reader, ref itemRest);

        reader.Read(); // consume array end

        value = new Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>(item1, item2, item3, item4, item5, item6, item7, itemRest);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1> : LuminPackParser<ValueTuple<T1?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?>(
            reader.ReadValue<T1>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?>(item1);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2> : LuminPackParser<ValueTuple<T1?, T2?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?>(item1, item2);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3> : LuminPackParser<ValueTuple<T1?, T2?, T3?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?>(item1, item2, item3);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?, T4?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?, T4?>(item1, item2, item3, item4);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?>(item1, item2, item3, item4, item5);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>(item1, item2, item3, item4, item5, item6);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6, T7> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
        writer.WriteValue(value.Item7);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>(),
            reader.ReadValue<T7>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
        var v7 = value.Item7;
        evaluator.CalculateValue(in v7);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v7 = value.Item7;
        parser7.SerializeJson(ref writer, ref v7);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);
        reader.Read();
        T7? item7 = default;
        parser7.DeserializeJson(ref reader, ref item7);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>(item1, item2, item3, item4, item5, item6, item7);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6, T7, TRest> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>
    where TRest : struct
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value.Equals(default))
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
        writer.WriteValue(value.Item4);
        writer.WriteValue(value.Item5);
        writer.WriteValue(value.Item6);
        writer.WriteValue(value.Item7);
        writer.WriteValue(value.Rest);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = default;
            reader.Advance(1);
            return;
        }

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>(
            reader.ReadValue<T1>(),
            reader.ReadValue<T2>(),
            reader.ReadValue<T3>(),
            reader.ReadValue<T4>(),
            reader.ReadValue<T5>(),
            reader.ReadValue<T6>(),
            reader.ReadValue<T7>(),
            reader.ReadValue<TRest>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        if (value.Equals(default))
        {
            evaluator += 1;
            return;
        }

        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
        var v3 = value.Item3;
        evaluator.CalculateValue(in v3);
        var v4 = value.Item4;
        evaluator.CalculateValue(in v4);
        var v5 = value.Item5;
        evaluator.CalculateValue(in v5);
        var v6 = value.Item6;
        evaluator.CalculateValue(in v6);
        var v7 = value.Item7;
        evaluator.CalculateValue(in v7);
        var vRest = value.Rest;
        evaluator.CalculateValue(in vRest);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        if (value.Equals(default))
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        var parserRest = LuminPackParseProvider.Cache<TRest>.Parser!;
        
        writer.SetFirstElement(true);
        var v1 = value.Item1;
        parser1.SerializeJson(ref writer, ref v1);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v2 = value.Item2;
        parser2.SerializeJson(ref writer, ref v2);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v3 = value.Item3;
        parser3.SerializeJson(ref writer, ref v3);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v4 = value.Item4;
        parser4.SerializeJson(ref writer, ref v4);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v5 = value.Item5;
        parser5.SerializeJson(ref writer, ref v5);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v6 = value.Item6;
        parser6.SerializeJson(ref writer, ref v6);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        var v7 = value.Item7;
        parser7.SerializeJson(ref writer, ref v7);
        var vRest = value.Rest;
        parserRest.SerializeJson(ref writer, ref vRest);

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var parser1 = LuminPackParseProvider.Cache<T1>.Parser!;
        var parser2 = LuminPackParseProvider.Cache<T2>.Parser!;
        var parser3 = LuminPackParseProvider.Cache<T3>.Parser!;
        var parser4 = LuminPackParseProvider.Cache<T4>.Parser!;
        var parser5 = LuminPackParseProvider.Cache<T5>.Parser!;
        var parser6 = LuminPackParseProvider.Cache<T6>.Parser!;
        var parser7 = LuminPackParseProvider.Cache<T7>.Parser!;
        var parserRest = LuminPackParseProvider.Cache<TRest>.Parser!;

        reader.Read();
        T1? item1 = default;
        parser1.DeserializeJson(ref reader, ref item1);
        reader.Read();
        T2? item2 = default;
        parser2.DeserializeJson(ref reader, ref item2);
        reader.Read();
        T3? item3 = default;
        parser3.DeserializeJson(ref reader, ref item3);
        reader.Read();
        T4? item4 = default;
        parser4.DeserializeJson(ref reader, ref item4);
        reader.Read();
        T5? item5 = default;
        parser5.DeserializeJson(ref reader, ref item5);
        reader.Read();
        T6? item6 = default;
        parser6.DeserializeJson(ref reader, ref item6);
        reader.Read();
        T7? item7 = default;
        parser7.DeserializeJson(ref reader, ref item7);
        reader.Read();
        TRest itemRest = default;
        parserRest.DeserializeJson(ref reader, ref itemRest);

        reader.Read(); // consume array end

        value = new ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>(item1, item2, item3, item4, item5, item6, item7, itemRest);
    }
}
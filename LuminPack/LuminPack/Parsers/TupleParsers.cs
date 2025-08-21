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
        
        if (value is null)
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
        if (value is null)
        {
            evaluator += 1;
            
            return;
        }

        var v = value.Item1;
        evaluator.CalculateValue(in v);
    }
}

[Preserve]
public sealed class TupleParser<T1, T2> : LuminPackParser<Tuple<T1?, T2?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
        {
            evaluator += 1;
            return;
        }

        
        var v1 = value.Item1;
        evaluator.CalculateValue(in v1);
        
        var v2 = value.Item2;
        evaluator.CalculateValue(in v2);
    }
}

// TupleParser<T1, T2, T3> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3> : LuminPackParser<Tuple<T1?, T2?, T3?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
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
}

// TupleParser<T1, T2, T3, T4> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3, T4> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
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
}

// TupleParser<T1, T2, T3, T4, T5> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
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
}

// TupleParser<T1, T2, T3, T4, T5, T6> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
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
}

// TupleParser<T1, T2, T3, T4, T5, T6, T7> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6, T7> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
        if (value is null)
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
}

// TupleParser<T1, T2, T3, T4, T5, T6, T7, TRest> 类
[Preserve]
public sealed class TupleParser<T1, T2, T3, T4, T5, T6, T7, TRest> : LuminPackParser<Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>> 
    where TRest : notnull
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
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
            reader.ReadValue<TRest>()!
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Tuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>? value)
    {
        if (value is null)
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
        
        var rest = value.Rest;
        evaluator.CalculateValue(in rest);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1> : LuminPackParser<ValueTuple<T1?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            return;
        }

        writer.WriteValue(value.Item1);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?>>())
        {
            reader.DangerousReadUnmanaged(out value);
            return;
        }

        value = new ValueTuple<T1?>(
            reader.ReadValue<T1>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ValueTuple<T1?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?>>();
            return;
        }
        
        evaluator.CalculateValue(in value.Item1);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2> : LuminPackParser<ValueTuple<T1?, T2?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3> : LuminPackParser<ValueTuple<T1?, T2?, T3?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            return;
        }

        writer.WriteValue(value.Item1);
        writer.WriteValue(value.Item2);
        writer.WriteValue(value.Item3);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ValueTuple<T1?, T2?, T3?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?>>())
        {
            writer.DangerousWriteUnmanaged(value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?, T4?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
        evaluator.CalculateValue(in value.Item4);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?>>())
        {
            writer.DangerousWriteUnmanaged(value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?, T4?, T5?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
        evaluator.CalculateValue(in value.Item4);
        evaluator.CalculateValue(in value.Item5);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>())
        {
            writer.DangerousWriteUnmanaged(value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
        evaluator.CalculateValue(in value.Item4);
        evaluator.CalculateValue(in value.Item5);
        evaluator.CalculateValue(in value.Item6);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6, T7> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>())
        {
            writer.DangerousWriteUnmanaged(value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
        evaluator.CalculateValue(in value.Item4);
        evaluator.CalculateValue(in value.Item5);
        evaluator.CalculateValue(in value.Item6);
        evaluator.CalculateValue(in value.Item7);
    }
}

[Preserve]
public sealed class ValueTupleParser<T1, T2, T3, T4, T5, T6, T7, TRest> : LuminPackParser<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>> 
    where TRest : struct
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest> value)
    {
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>())
        {
            writer.DangerousWriteUnmanaged(value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>())
        {
            reader.DangerousReadUnmanaged(out value);
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
        if (!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>())
        {
            evaluator += evaluator.DangerousCalculateUnmanaged<ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, TRest>>();
            return;
        }

        evaluator.CalculateValue(in value.Item1);
        evaluator.CalculateValue(in value.Item2);
        evaluator.CalculateValue(in value.Item3);
        evaluator.CalculateValue(in value.Item4);
        evaluator.CalculateValue(in value.Item5);
        evaluator.CalculateValue(in value.Item6);
        evaluator.CalculateValue(in value.Item7);
        evaluator.CalculateValue(in value.Rest);
    }
}
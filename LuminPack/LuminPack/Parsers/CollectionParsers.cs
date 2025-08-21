using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Interface;

namespace LuminPack.Parsers
{
    [Preserve]
    public static class ListParser
    {
        [Preserve]
        private const uint maxDepth = 3;
        
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializePackable<T>(ref LuminPackWriter writer, List<T?>? value)
        {
            if (value is null)
            {
                var index = writer.CurrentIndex;
                
                writer.WriteNullCollectionHeader(ref index);

                writer.Advance(4);
                
                return;
            }
            
            writer.WriteSpan(LuminPackMarshal.GetListSpan(ref value));
        }

        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? DeserializePackable<T>(ref LuminPackReader reader)
        {
            List<T?>? value = default;
            DeserializePackable(ref reader, ref value);
            return value;
        }

        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeserializePackable<T>(ref LuminPackReader reader, scoped ref List<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;

                reader.Advance(4);
                
                return;
            }
            
            if (value is null)
            {
                value = new List<T?>(length);
            }
            else if (value.Count == length)
            {
                value.Clear();
            }
            
            var span = LuminPackMarshal.GetListSpan(ref value, length);
            
            reader.Advance(4);
            reader.ReadSpan(ref index, length, ref span);
            
        }

        #region IList
        
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? GetUnderlyingIList<T>(Collection<T?>? value, ref int depth)
        {
            
            if (depth >= maxDepth) return null;
            
            var view = LuminPackMarshal.As<Collection<T?>, CollectionIListView<T?>>(ref value);
            
            switch (view.items)
            {
                case List<T?> list: return list;
                case ReadOnlyCollectionBuilder<T?> readOnlyCollectionBuilder: 
                    return GetUnderlyingIList(readOnlyCollectionBuilder);
                case ReadOnlyObservableCollection<T?> readOnlyObservableCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyObservableCollection, ref depth);
                    break;
                case ObservableCollection<T?> observableCollection:
                    depth++;
                    GetUnderlyingIList(observableCollection, ref depth);
                    break;
                case ReadOnlyCollection<T?> readOnlyCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyCollection, ref depth);
                    break;
                case Collection<T?> collection:
                    depth++;
                    GetUnderlyingIList(collection, ref depth);
                    break;
            }
            
            return null;
        }

        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? GetUnderlyingIList<T>(ReadOnlyCollection<T?>? value, ref int depth)
        {
            if (depth >= maxDepth) return null;
            
            var view = LuminPackMarshal.As<ReadOnlyCollection<T?>, ReadOnlyCollectionIListView<T?>>(ref value);
            
            switch (view.items)
            {
                case List<T?> list: return list;
                case ReadOnlyCollectionBuilder<T?> readOnlyCollectionBuilder: 
                    return GetUnderlyingIList(readOnlyCollectionBuilder);
                case ReadOnlyObservableCollection<T?> readOnlyObservableCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyObservableCollection, ref depth);
                    break;
                case ObservableCollection<T?> observableCollection:
                    depth++;
                    GetUnderlyingIList(observableCollection, ref depth);
                    break;
                case ReadOnlyCollection<T?> readOnlyCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyCollection, ref depth);
                    break;
                case Collection<T?> collection:
                    depth++;
                    GetUnderlyingIList(collection, ref depth);
                    break;
            }
            
            return null;
        }
        
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? GetUnderlyingIList<T>(ObservableCollection<T?>? value, ref int depth)
        {
            if (depth >= maxDepth) return null;
            
            var view = LuminPackMarshal.As<ObservableCollection<T?>, ObservableCollectionIListView<T?>>(ref value);
            
            switch (view.items)
            {
                case List<T?> list: return list;
                case ReadOnlyCollectionBuilder<T?> readOnlyCollectionBuilder: 
                    return GetUnderlyingIList(readOnlyCollectionBuilder);
                case ReadOnlyObservableCollection<T?> readOnlyObservableCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyObservableCollection, ref depth);
                    break;
                case ObservableCollection<T?> observableCollection:
                    depth++;
                    GetUnderlyingIList(observableCollection, ref depth);
                    break;
                case ReadOnlyCollection<T?> readOnlyCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyCollection, ref depth);
                    break;
                case Collection<T?> collection:
                    depth++;
                    GetUnderlyingIList(collection, ref depth);
                    break;
            }
            
            return null;
        }
        
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? GetUnderlyingIList<T>(ReadOnlyObservableCollection<T?>? value, ref int depth)
        {
            if (depth >= maxDepth) return null;
            
            var observable = 
                LuminPackMarshal.As<ReadOnlyObservableCollection<T?>, ReadOnlyObservableCollectionView<T?>>(ref value);

            var view = 
                LuminPackMarshal.As<ObservableCollection<T?>, ObservableCollectionIListView<T?>>(ref observable.items);
            
            switch (view.items)
            {
                case List<T?> list: return list;
                case ReadOnlyCollectionBuilder<T?> readOnlyCollectionBuilder:
                    return GetUnderlyingIList(readOnlyCollectionBuilder);
                case ReadOnlyObservableCollection<T?> readOnlyObservableCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyObservableCollection, ref depth);
                    break;
                case ObservableCollection<T?> observableCollection:
                    depth++;
                    GetUnderlyingIList(observableCollection, ref depth);
                    break;
                case ReadOnlyCollection<T?> readOnlyCollection:
                    depth++;
                    GetUnderlyingIList(readOnlyCollection, ref depth);
                    break;
                case Collection<T?> collection:
                    depth++;
                    GetUnderlyingIList(collection, ref depth);
                    break;
            }
            
            return null;
        }
        
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? GetUnderlyingIList<T>(ReadOnlyCollectionBuilder<T?>? value)
        {
            return LuminPackMarshal.As<ReadOnlyCollectionBuilder<T?>, List<T?>>(ref value);
        }
        
        #endregion
        
    }

    [Preserve]
    public sealed class ListParser<T> : LuminPackParser<List<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref List<T?>? value)
        {
            
            if (value is null)
            {
                var index = writer.GetCurrentSpanIndex();
                
                writer.WriteNullCollectionHeader(ref index);

                writer.Advance(4);
                
                return;
            }
            
            writer.WriteSpan(LuminPackMarshal.GetListSpan(ref value));
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref List<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;

                reader.Advance(4);
                
                return;
            }
            
            if (value is null)
            {
                value = new List<T?>(length);
            }
            else if (value.Count == length)
            {
                value.Clear();
            }
            
            var span = LuminPackMarshal.GetListSpan(ref value, length);
            
            reader.Advance(4);
            
            reader.ReadSpan(ref index, length, ref span);
            
            
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref List<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var span = LuminPackMarshal.GetListSpan(ref value);
            evaluator.CalculateSpan(ref span);
        }
    }
    
    [Preserve]
    public sealed class DictionaryParser<TKey, TValue> : LuminPackParser<Dictionary<TKey, TValue?>>
        where TKey : notnull
    {
        [Preserve]
        private static DictionaryParser<TKey, TValue> Instance { get; } = 
            new DictionaryParser<TKey, TValue>();
        
        [Preserve]
        private readonly IEqualityComparer<TKey>? _equalityComparer;

        static DictionaryParser()
        {
            LuminPackParseProvider.RegisterParsers(Instance);
        }
        
        public DictionaryParser()
            : this(null)
        {

        }

        public DictionaryParser(IEqualityComparer<TKey>? equalityComparer)
        {
            _equalityComparer = equalityComparer;
        }
        

        [Preserve]
        public override void Serialize(scoped ref LuminPackWriter writer, scoped ref Dictionary<TKey, TValue?>? value)
        {
            var index = writer.CurrentIndex;
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var keyFormatter = writer.GetParser<TKey>();
            var valueFormatter = writer.GetParser<TValue>();

            writer.WriteCollectionHeader(ref index, value.Count);
            writer.Advance(4);
            foreach (var item in value)
            {
                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref Dictionary<TKey, TValue?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            if (value is null)
            {
                value = new Dictionary<TKey, TValue?>(length, _equalityComparer);
            }
            else
            {
                value.Clear();
            }

            reader.Advance(4);
            var keyFormatter = reader.GetParser<TKey>();
            var valueFormatter = reader.GetParser<TValue>();
            for (int i = 0; i < length; i++)
            {
                KeyValuePairParser.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                value.Add(k!, v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Dictionary<TKey, TValue?>? value)
        {
            if (value is null)
            {
                evaluator += 4;
                
                return;
            }
            
            var keyFormatter = evaluator.GetEvaluator<TKey>();
            var valueFormatter = evaluator.GetEvaluator<TValue>();
            
            evaluator += 4;
            foreach (var item in value)
            {
                var k = item.Key;
                var v = item.Value;
                KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
            }
            
        }
    }

    [Preserve]
    public sealed class ConcurrentDictionaryParser<TKey, TValue> : LuminPackParser<ConcurrentDictionary<TKey, TValue?>> 
        where TKey : notnull
    {
        readonly IEqualityComparer<TKey>? _equalityComparer;

        public ConcurrentDictionaryParser()
            : this(null)
        {

        }

        public ConcurrentDictionaryParser(IEqualityComparer<TKey>? equalityComparer)
        {
            _equalityComparer = equalityComparer;
        }

        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ConcurrentDictionary<TKey, TValue?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var keyFormatter = writer.GetParser<TKey>();
            var valueFormatter = writer.GetParser<TValue>();

            writer.WriteCollectionHeader(ref index, value.Count);
            writer.Advance(4);
            
            var count = value.Count;
            var i = 0;
            foreach (var item in value)
            {
                i++;
                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
            }
            
            if (i != count) 
                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ConcurrentDictionary<TKey, TValue?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            if (value is null)
            {
                value = new ConcurrentDictionary<TKey, TValue?>(_equalityComparer);
            }
            else
            {
                value.Clear();
            }
            
            reader.Advance(4);
            
            var keyFormatter = reader.GetParser<TKey>();
            var valueFormatter = reader.GetParser<TValue>();
            for (int i = 0; i < length; i++)
            {
                KeyValuePairParser.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                value.TryAdd(k!, v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ConcurrentDictionary<TKey, TValue?>? value)
        {
            if (value is null)
            {
                evaluator += 4;
                
                return;
            }
            
            var keyFormatter = evaluator.GetEvaluator<TKey>();
            var valueFormatter = evaluator.GetEvaluator<TValue>();
            
            evaluator += 4;
            
            var count = value.Count;
            var i = 0;
            
            foreach (var item in value)
            {
                i++;
                var k = item.Key;
                var v = item.Value;
                KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
            }
            
            if (i != count) 
                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }
    }

    [Preserve]
    public sealed class SortedDictionaryParser<TKey, TValue> : LuminPackParser<SortedDictionary<TKey, TValue?>> 
        where TKey : notnull
    {
        
        readonly IComparer<TKey>? comparer;

        public SortedDictionaryParser()
            : this(null)
        {

        }

        public SortedDictionaryParser(IComparer<TKey>? comparer)
        {
            this.comparer = comparer;
        }
        
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref SortedDictionary<TKey, TValue?>? value)
        {
            ref var index  = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var keyFormatter = writer.GetParser<TKey>();
            var valueFormatter = writer.GetParser<TValue>();

            writer.WriteCollectionHeader(ref index, value.Count);
            writer.Advance(4);
            foreach (var item in value)
            {
                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref SortedDictionary<TKey, TValue?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new SortedDictionary<TKey, TValue?>(comparer);
            }
            else
            {
                value.Clear();
            }

            var keyFormatter = reader.GetParser<TKey>();
            var valueFormatter = reader.GetParser<TValue>();
            for (var i = 0; i < length; i++)
            {
                KeyValuePairParser.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                value.Add(k!, v);
            }

        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref SortedDictionary<TKey, TValue?>? value)
        {
            if (value is null)
            {
                evaluator += 4;
                
                return;
            }
            
            var keyFormatter = evaluator.GetEvaluator<TKey>();
            var valueFormatter = evaluator.GetEvaluator<TValue>();
            
            evaluator += 4;
            foreach (var item in value)
            {
                var k = item.Key;
                var v = item.Value;
                KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
            }
        }
    }

    [Preserve]
    public sealed class StackParser<T> : LuminPackParser<Stack<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref Stack<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);

                index += 4;
                
                return;
            }

            var span = LuminPackMarshal.GetStackSpan(ref value);
            
            writer.WriteSpan(ref index, span);
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref Stack<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();

            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }
            
            reader.Advance(4);
            
            if (value is null)
            {
                value = new Stack<T?>(length);
            }
            else
            {
                value.Clear();
            }
            
            var span = LuminPackMarshal.GetStackSpan(ref value, length);
            
            reader.ReadSpan(ref index, length, ref span);
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Stack<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var span = LuminPackMarshal.GetStackSpan(ref value);
            evaluator.CalculateSpan(ref span);
            
        }
    }

    [Preserve]
    public sealed class QueueParser<T> : LuminPackParser<Queue<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref Queue<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);

                index += 4;
                
                return;
            }

            var span = LuminPackMarshal.GetQueueSpan(ref value, value.Count);
            
            LuminPackMarshal.GetQueueSize(ref value, out var head, out var tail, out var size);
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                if (span.IsEmpty)
                {
                    writer.WriteCollectionHeader(ref index, 0);
                    
                    writer.Advance(4);
                    
                    return;
                }
            
                writer.WriteCollectionHeader(ref index, span.Length);
                
                writer.Advance(4);
                
                writer.WriteUnmanaged(head);
            
                writer.Advance(4);

                writer.WriteUnmanaged(tail);
            
                writer.Advance(4);
            
                writer.WriteUnmanaged(size);
            
                writer.Advance(4);
            
                var srcLength = Unsafe.SizeOf<T>() * span.Length;

                ref var dest = ref writer.GetSpanReference(index);
                ref var src = ref Unsafe.As<T, byte>(ref span.GetPinnableReference()!);
            
                Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)srcLength);
            
                writer.Advance(srcLength);
                
                return;
            }

            if (span.IsEmpty)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }
            
            var parser = writer.GetParser<T>();
            
            
            writer.WriteCollectionHeader(ref index, span.Length);
            
            writer.Advance(4);

            writer.WriteUnmanaged(head);
            
            writer.Advance(4);

            writer.WriteUnmanaged(tail);
            
            writer.Advance(4);
            
            writer.WriteUnmanaged(size);
            
            writer.Advance(4);
            
            foreach (var item in span)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }

        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref Queue<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();

            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }
            
            reader.Advance(4);
            
            if (value is null)
            {
                value = new Queue<T?>(length);
            }
            else
            {
                value.Clear();
#if NET8_0_OR_GREATER
                value.EnsureCapacity(length);
#endif
            }


            var span = LuminPackMarshal.GetQueueSpan(ref value, length);

            reader.ReadUnmanaged(out int head);
            
            reader.Advance(4);

            reader.ReadUnmanaged(out int tail);
            
            reader.Advance(4);
            
            reader.ReadUnmanaged(out int size);
            
            reader.Advance(4);
            
            LuminPackMarshal.SetQueueSize(ref value, head, tail, size);
            
            if (length is 0)
            {
                return;
            }
            
            if (span.Length != length)
            {
                span = LuminPackMarshal.AllocateUninitializedArray<T>(length);
            }
            
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                ref var dest = ref LuminPackMarshal.GetReference(ref span.GetPinnableReference());
            
                var srcLength = length * Unsafe.SizeOf<T>();

                Unsafe.CopyBlockUnaligned(ref dest, ref reader.GetSpanReference(index), (uint)srcLength);
                
                reader.Advance(srcLength);
                
                return;
            }
            
            

            var parser = reader.GetParser<T>();
            

            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref span[i]);
            }
            
            
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Queue<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }


            var span = LuminPackMarshal.GetQueueSpan(ref value);
            evaluator.CalculateSpan(ref span);

            //head, tail and size
            evaluator += 12;
        }
    }

    [Preserve]
    public sealed class LinkedListParser<T> : LuminPackParser<LinkedList<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref LinkedList<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<T?>();
                
            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref LinkedList<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new LinkedList<T?>();
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.AddLast(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref LinkedList<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T?>();
            
            evaluator += 4;
            
            foreach (var item in value)
            {
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
        }
    }

    [Preserve]
    public sealed class HashSetParser<T> : LuminPackParser<HashSet<T?>>
    {
        readonly IEqualityComparer<T?>? equalityComparer;

        public HashSetParser()
            : this(null)
        {
        }

        public HashSetParser(IEqualityComparer<T?>? equalityComparer)
        {
            this.equalityComparer = equalityComparer;
        }
        
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref HashSet<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<T?>();
            
            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref HashSet<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }
            
            reader.Advance(4);

            if (value is null)
            {
                value = new HashSet<T?>(length, equalityComparer);
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Add(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref HashSet<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T?>();
            
            evaluator += 4;
            
            foreach (var item in value)
            {
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
        }
    }

    [Preserve]
    public sealed class SortedSetParser<T> : LuminPackParser<SortedSet<T?>>
    {
        
        readonly IComparer<T?>? comparer;

        public SortedSetParser()
            : this(null)
        {
        }

        public SortedSetParser(IComparer<T?>? comparer)
        {
            this.comparer = comparer;
        }
        
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref SortedSet<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<T?>();
            
            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref SortedSet<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new SortedSet<T?>(comparer);
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Add(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref SortedSet<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T?>();
            
            evaluator += 4;
            
            foreach (var item in value)
            {
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
    
#if NET7_0_OR_GREATER
    [Preserve]
    public sealed class PriorityQueueParser<TElement, TPriority> : LuminPackParser<PriorityQueue<TElement?, TPriority?>>
    {
        static PriorityQueueParser()
        {
            if (!LuminPackParseProvider.IsRegistered<(TElement?, TPriority?)>())
            {
                //LuminPackParseProvider.Register(new ValueTupleFormatter<TElement?, TPriority?>());
            }
        }
        
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref PriorityQueue<TElement?, TPriority?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<(TElement?, TPriority?)>();

            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value.UnorderedItems)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref PriorityQueue<TElement?, TPriority?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new PriorityQueue<TElement?, TPriority?>(length);
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<(TElement?, TPriority?)>();
            for (int i = 0; i < length; i++)
            {
                (TElement?, TPriority?) v = default;
                parser.Deserialize(ref reader, ref v);
                value.Enqueue(v.Item1, v.Item2);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref PriorityQueue<TElement?, TPriority?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<(TElement?, TPriority?)>();
            
            evaluator += 4;
            
            foreach (var item in value.UnorderedItems)
            {
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
#endif
    
    [Preserve]
    public sealed class CollectionParser<T> : LuminPackParser<Collection<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref Collection<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var depth = 0;
            
            var list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                ListParser.SerializePackable(ref writer, list);
            }
            else
            {
                
                writer.WriteCollectionHeader(ref index, value.Count);
                
                writer.Advance(4);
                
                var parser = writer.GetParser<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    parser.Serialize(ref writer, ref temp);
                }
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref Collection<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }
            
            reader.Advance(4);
            
            if (value is null)
            {
                value = new Collection<T?>(new List<T?>(length));
            }
            else
            {
                value.Clear();
            }
            
            var list = LuminPackMarshal.As<Collection<T?>, CollectionView<T?>>(ref value);
            
            var span = LuminPackMarshal.GetListSpan(ref list.items!, length);
            
            reader.ReadSpan(ref index, length, ref span);
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Collection<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var depth = 0;
            
            List<T?>? list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                var span = LuminPackMarshal.GetListSpan(ref list);
                
                evaluator.CalculateSpan(ref span);
            }
            else
            {
                evaluator += 4;
                
                var eval = evaluator.GetEvaluator<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    eval.CalculateOffset(ref evaluator, ref temp);
                }
            }
        }
        
    }

    [Preserve]
    public sealed class ObservableCollectionParser<T> : LuminPackParser<ObservableCollection<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ObservableCollection<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var depth = 0;
            
            var list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                ListParser.SerializePackable(ref writer, list);
            }
            else
            {
                writer.WriteCollectionHeader(ref index, value.Count);
                
                writer.Advance(4);
                
                var parser = writer.GetParser<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    parser.Serialize(ref writer, ref temp);
                }
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ObservableCollection<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }
            
            reader.Advance(4);
            
            if (value is null)
            {
                value = new ObservableCollection<T?>();
            }
            else
            {
                value.Clear();
            }
            
            var list = 
                LuminPackMarshal.As<ObservableCollection<T?>, ObservableCollectionView<T?>>(ref value);

            list.items = new List<T?>(length);
            
            var span = LuminPackMarshal.GetListSpan(ref list.items!, length);
            
            reader.ReadSpan(ref index, length, ref span);
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ObservableCollection<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var depth = 0;
            
            List<T?>? list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                var span = LuminPackMarshal.GetListSpan(ref list);
                
                evaluator.CalculateSpan(ref span);
            }
            else
            {
                evaluator += 4;
                
                var eval = evaluator.GetEvaluator<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    eval.CalculateOffset(ref evaluator, ref temp);
                }
            }
        }
        
        
    }

    [Preserve]
    public sealed class ConcurrentQueueParser<T> : LuminPackParser<ConcurrentQueue<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ConcurrentQueue<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }
            

            var parser = writer.GetParser<T?>();
            
            var count = value.Count;
            
            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            var i = 0;
            foreach (var item in value)
            {
                i++;
                var v = item;
                parser.Serialize(ref writer, ref v);
            }

            if (i != count) LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ConcurrentQueue<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new ConcurrentQueue<T?>();
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Enqueue(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ConcurrentQueue<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T>();
            
            evaluator += 4;
            
            var count = value.Count;
            
            var i = 0;
            foreach (var item in value)
            {
                i++;
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
            
            if (i != count) LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }
    }

    [Preserve]
    public sealed class ConcurrentStackParser<T> : LuminPackParser<ConcurrentStack<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ConcurrentStack<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            // reverse order in serialize
            var count = value.Count;
            T?[] rentArray = ArrayPool<T?>.Shared.Rent(count);
            try
            {
                var i = 0;
                foreach (var item in value)
                {
                    rentArray[i++] = item;
                }
                if (i != count) LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();

                var formatter = writer.GetParser<T?>();
                
                writer.WriteCollectionHeader(ref index, count);
                
                writer.Advance(4);
                
                for (i = i - 1; i >= 0; i--)
                {
                    formatter.Serialize(ref writer, ref rentArray[i]);
                }
            }
            finally
            {
                ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ConcurrentStack<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new ConcurrentStack<T?>();
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Push(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ConcurrentStack<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T>();
            
            evaluator += 4;
            
            var count = value.Count;
            
            var i = 0;
            foreach (var item in value)
            {
                i++;
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
            
            if (i != count) LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }
    }

    [Preserve]
    public sealed class ConcurrentBagParser<T> : LuminPackParser<ConcurrentBag<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ConcurrentBag<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<T?>();
            
            var count = value.Count;
            
            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            var i = 0;
            foreach (var item in value)
            {
                i++;
                var v = item;
                parser.Serialize(ref writer, ref v);
            }

            if (i != count) 
                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ConcurrentBag<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new ConcurrentBag<T?>();
            }
            else
            {
                value.Clear();
            }

            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Add(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ConcurrentBag<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T>();
            
            evaluator += 4;
            
            var count = value.Count;
            var i = 0;
            foreach (var item in value)
            {
                i++;
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
            
            if (i != count) 
                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
        }
    }

    [Preserve]
    public sealed class SortedListParser<TKey, TValue> : LuminPackParser<SortedList<TKey, TValue?>> 
        where TKey : notnull
    {
        readonly IComparer<TKey>? comparer;

        public SortedListParser()
            : this(null)
        {
            
        }

        public SortedListParser(IComparer<TKey>? comparer)
        {
            this.comparer = comparer;
        }
        
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref SortedList<TKey, TValue?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var keyFormatter = writer.GetParser<TKey>();
            var valueFormatter = writer.GetParser<TValue>();

            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref SortedList<TKey, TValue?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            if (value is null)
            {
                value = new SortedList<TKey, TValue?>(length, comparer);
            }
            else
            {
                value.Clear();
            }

            var keyFormatter = reader.GetParser<TKey>();
            var valueFormatter = reader.GetParser<TValue>();
            for (int i = 0; i < length; i++)
            {
                KeyValuePairParser.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                value.Add(k!, v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref SortedList<TKey, TValue?>? value)
        {
            if (value is null)
            {
                evaluator += 4;
                
                return;
            }
            
            var keyFormatter = evaluator.GetEvaluator<TKey>();
            var valueFormatter = evaluator.GetEvaluator<TValue>();
            
            evaluator += 4;
            foreach (var item in value)
            {
                var k = item.Key;
                var v = item.Value;
                KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
            }
        }
    }

    [Preserve]
    public sealed class ReadOnlyCollectionParser<T> : LuminPackParser<ReadOnlyCollection<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyCollection<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var depth = 0;
            
            var list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                ListParser.SerializePackable(ref writer, list);
            }
            else
            {
                writer.WriteCollectionHeader(ref index, value.Count);
                
                writer.Advance(4);
                
                var parser = writer.GetParser<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    parser.Serialize(ref writer, ref temp);
                }
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyCollection<T?>? value)
        {
            var array = reader.ReadArray<T?>();

            if (array is null)
            {
                value = null;
            }
            else
            {
                value = new ReadOnlyCollection<T?>(array);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlyCollection<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var depth = 0;
            
            List<T?>? list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                var span = LuminPackMarshal.GetListSpan(ref list);
                
                evaluator.CalculateSpan(ref span);
            }
            else
            {
                evaluator += 4;
                
                var eval = evaluator.GetEvaluator<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    eval.CalculateOffset(ref evaluator, ref temp);
                }
            }
        }
        
    }

    [Preserve]
    public sealed class ReadOnlyObservableCollectionParser<T> : LuminPackParser<ReadOnlyObservableCollection<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyObservableCollection<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var depth = 0;
            
            var list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                ListParser.SerializePackable(ref writer, list);
            }
            else
            {
                writer.WriteCollectionHeader(ref index, value.Count);
                
                writer.Advance(4);
                
                var parser = writer.GetParser<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    parser.Serialize(ref writer, ref temp);
                }
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyObservableCollection<T?>? value)
        {
            var array = reader.ReadArray<T?>();
            
            if (array is null)
            {
                value = null;
            }
            else
            {
                value = new ReadOnlyObservableCollection<T?>(new ObservableCollection<T?>(array));
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlyObservableCollection<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var depth = 0;
            
            List<T?>? list = ListParser.GetUnderlyingIList(value, ref depth);
            
            if (list != null)
            {
                var span = LuminPackMarshal.GetListSpan(ref list);
                
                evaluator.CalculateSpan(ref span);
            }
            else
            {
                evaluator += 4;
                
                var eval = evaluator.GetEvaluator<T?>();
                
                foreach (var item in value)
                {
                    var temp = item;
                    eval.CalculateOffset(ref evaluator, ref temp);
                }
            }
        }
        
    }

    [Preserve]
    public sealed class BlockingCollectionParser<T> : LuminPackParser<BlockingCollection<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref BlockingCollection<T?>? value)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            if (value is null)
            {
                writer.WriteNullCollectionHeader(ref index);
                
                writer.Advance(4);
                
                return;
            }

            var parser = writer.GetParser<T?>();
            
            writer.WriteCollectionHeader(ref index, value.Count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref BlockingCollection<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;
                
                reader.Advance(4);
                
                return;
            }

            reader.Advance(4);
            
            value = new BlockingCollection<T?>();

            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                T? v = default;
                parser.Deserialize(ref reader, ref v);
                value.Add(v);
            }
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref BlockingCollection<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var eval = evaluator.GetEvaluator<T?>();

            evaluator += 4;
            
            foreach (var item in value)
            {
                var v = item;
                eval.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
    
    [Preserve]
    public sealed class ReadOnlyCollectionBuilderParser<T> : LuminPackParser<ReadOnlyCollectionBuilder<T?>>
    {
        [Preserve]
        public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyCollectionBuilder<T?>? value)
        {
            
            if (value is null)
            {
                var index = writer.CurrentIndex;
                
                writer.WriteNullCollectionHeader(ref index);

                writer.Advance(4);
                
                return;
            }
            
            var list = LuminPackMarshal.As<ReadOnlyCollectionBuilder<T?>, List<T?>>(ref value);
            
            writer.WriteSpan(LuminPackMarshal.GetListSpan(ref list));
        }

        [Preserve]
        public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyCollectionBuilder<T?>? value)
        {
            ref var index = ref reader.GetCurrentSpanOffset();
            
            if (!reader.TryReadCollectionHead(ref index, out var length))
            {
                value = null;

                reader.Advance(4);
                
                return;
            }
            
            if (value is null)
            {
                value = new ReadOnlyCollectionBuilder<T?>(length);
            }
            else if (value.Count == length)
            {
                value.Clear();
            }
            
            var list = LuminPackMarshal.As<ReadOnlyCollectionBuilder<T?>, List<T?>>(ref value);
            
            var span = LuminPackMarshal.GetListSpan(ref list, length);
            
            reader.Advance(4);
            
            reader.ReadSpan(ref index, length, ref span);
            
            
        }

        [Preserve]
        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlyCollectionBuilder<T?>? value)
        {
            if (value is null || value.Count == 0)
            {
                evaluator += 4;
                
                return;
            }
            
            var list = LuminPackMarshal.As<ReadOnlyCollectionBuilder<T?>, List<T?>>(ref value);
            
            var span = LuminPackMarshal.GetListSpan(ref list);
            
            evaluator.CalculateSpan(ref span);
        }
        
    }
    
    internal class CollectionIListView<TValue>
    {
        public IList<TValue> items;
    }
    
    internal class CollectionView<TValue>
    {
        public List<TValue> items;
    }
    
    internal class ObservableCollectionView<TValue>
    {
        public List<TValue> items;
    }
    
    internal class ObservableCollectionIListView<TValue>
    {
        public IList<TValue> items;
    }
    
    internal class ReadOnlyCollectionView<TValue>
    {
        public List<TValue> items;
    }
    
    internal class ReadOnlyCollectionIListView<TValue>
    {
        public IList<TValue> items;
    }
    
    internal class ReadOnlyObservableCollectionView<TCollection>
    {
        public ObservableCollection<TCollection> items;
    }
    
    internal class ReadOnlyCollectionBuilderView<TValue>
    {
        public TValue[] items;
        public int size;
    }
}
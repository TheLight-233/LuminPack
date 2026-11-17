#if NET10_0_OR_GREATER
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Utility;

public static partial class LuminTreeStorage
{
    /// <summary>Binds the private wrapper for a completed hash tree and its default comparer.</summary>
    /// <typeparam name="T">The set element type.</typeparam>
    private static class ImmutableHashSetConstructors<T>
    {
        /// <summary>Wraps an already frozen tree and its element count.</summary>
        internal delegate ImmutableHashSet<T> WrapTree(object root, IEqualityComparer<T> comparer, int count);

        internal static readonly WrapTree? Wrap;
        internal static readonly object? EmptyTree;
        internal static readonly object? EmptyList;
        internal static readonly IEqualityComparer<T> Comparer = ImmutableHashSet<T>.Empty.KeyComparer;

        /// <summary>Attempts the optional binding where dynamic code is compiled.</summary>
        static ImmutableHashSetConstructors()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
                return;
            try
            {
                Wrap = Bind(out EmptyTree, out EmptyList);
            }
            catch
            {
                Wrap = null;
            }
        }

        /// <summary>Validates the constructor and the shared empty tree and collision-list nodes.</summary>
        /// <param name="emptyTree">Receives the default set's empty tree.</param>
        /// <param name="emptyList">Receives the collision list's shared empty node.</param>
        /// <returns>The bound wrapper, or null when the shape differs.</returns>
        [RequiresDynamicCode("The verified constructor is called from a DynamicMethod.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Missing private metadata leaves the constructor unbound.")]
        private static WrapTree? Bind(out object? emptyTree, out object? emptyList)
        {
            emptyTree = null;
            emptyList = null;
            if (!ReferenceEquals(Comparer, EqualityComparer<T>.Default))
                return null;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var set = typeof(ImmutableHashSet<T>);
            var rootField = set.GetField("_root", instance);
            if (rootField is null || rootField.FieldType.IsValueType)
                return null;
            var tree = rootField.FieldType;
            var constructor = set.GetConstructor(instance, [tree, typeof(IEqualityComparer<T>), typeof(int)]);
            var listNode = FieldType(FieldType(tree, "_value"), "_additionalElements");
            var listEmpty = listNode?.GetField("EmptyNode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object? treeSentinel = rootField.GetValue(ImmutableHashSet<T>.Empty);
            object? listSentinel = listEmpty?.GetValue(null);
            if (constructor is null || treeSentinel is null || listSentinel is null || listEmpty!.FieldType != listNode)
                return null;

            var create = new DynamicMethod("LuminImmutableHashSetWrap_" + typeof(T).Name, set,
                [typeof(object), typeof(IEqualityComparer<T>), typeof(int)], typeof(ImmutableHashSetConstructors<T>).Module, true);
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, tree);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            var wrap = create.CreateDelegate<WrapTree>();
            var probe = wrap(treeSentinel, Comparer, 0);
            if (probe.Count != 0 || !ReferenceEquals(rootField.GetValue(probe), treeSentinel) || !ReferenceEquals(probe.KeyComparer, Comparer))
                return null;
            emptyTree = treeSentinel;
            emptyList = listSentinel;
            return wrap;
        }
    }

    /// <summary>Checks a wrapped nonempty tree through public enumeration and persistent mutation.</summary>
    private static class ImmutableHashSetConstructorLayout
    {
        internal static readonly bool Available = Check(Probe);

        /// <summary>Checks the bound constructor independently of the fallback layout probe.</summary>
        /// <returns>Whether the wrapper preserves the linked tree and default comparer.</returns>
        private static bool Probe()
        {
            if (!ImmutableHashSetLayout<int>.Available || ImmutableHashSetConstructors<int>.Wrap is not { } wrap)
                return false;
            int[] items = [-7, 4, 11, 19, 20];
            long[] hashes = ArrayPool<long>.Shared.Rent(items.Length);
            try
            {
                if (!TrySortHashes(items, items.Length, hashes))
                    return false;
                var root = LinkHash(ImmutableHashSetConstructors<int>.EmptyTree!, ImmutableHashSetConstructors<int>.EmptyList!,
                    in ImmutableHashSetLayout<int>.Hash, items, hashes, 0, items.Length - 1);
                var wrapped = wrap(root, ImmutableHashSetConstructors<int>.Comparer, items.Length);
                var expected = ImmutableHashSet.CreateRange(items);
                return wrapped.Count == expected.Count && wrapped.SequenceEqual(expected) && wrapped.Contains(4) &&
                    wrapped.Remove(4).Add(27).SequenceEqual(expected.Remove(4).Add(27)) && wrapped.Contains(4);
            }
            finally
            {
                ArrayPool<long>.Shared.Return(hashes);
            }
        }
    }
}
#endif

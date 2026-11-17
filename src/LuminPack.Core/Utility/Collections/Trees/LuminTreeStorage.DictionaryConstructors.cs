#if NET10_0_OR_GREATER
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Utility;

public static partial class LuminTreeStorage
{
    /// <summary>Calls the private constructors of an immutable dictionary's hash tree node and of the dictionary itself.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <remarks>An uninitialized-object request costs about three times a constructor call for the tree nodes, and the singleton
    /// that otherwise provides the root and its shared fields costs a public insertion. The node constructor is the one the
    /// framework uses for its empty node, and every field the tree uses is assigned afterwards; the dictionary constructor is
    /// the one the framework wraps a finished tree with. Both run only where dynamic code is compiled; the bindings are
    /// checked once, and other runtimes, shapes or failures keep the field-linked build.</remarks>
    private static class ImmutableDictionaryConstructors<TKey, TValue> where TKey : notnull
    {
        /// <summary>Wraps a frozen tree, the default comparers and the pair count into a dictionary.</summary>
        internal delegate ImmutableDictionary<TKey, TValue> WrapTree(object root, object comparers, int count);

        /// <summary>Allocates a tree node through its parameterless constructor, or null when unbound.</summary>
        internal static readonly Func<object>? Node;

        /// <summary>Calls the dictionary's tree constructor, or null when unbound.</summary>
        internal static readonly WrapTree? Wrap;

        /// <summary>The empty dictionary's shared empty tree node.</summary>
        internal static readonly object? EmptyTree;

        /// <summary>The shared empty list node of single-pair buckets.</summary>
        internal static readonly object? EmptyList;

        /// <summary>The empty dictionary's default comparers.</summary>
        internal static readonly object? Comparers;

        /// <summary>Binds the constructors where dynamic code is compiled.</summary>
        static ImmutableDictionaryConstructors()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
                return;
            try
            {
                Bind(out Node, out Wrap, out EmptyTree, out EmptyList, out Comparers);
            }
            catch
            {
                Node = null;
                Wrap = null;
            }
        }

        /// <summary>Binds and checks both constructors and the shared nodes they are used with.</summary>
        /// <param name="node">Receives the node allocator.</param>
        /// <param name="wrap">Receives the dictionary constructor.</param>
        /// <param name="emptyTree">Receives the shared empty tree node.</param>
        /// <param name="emptyList">Receives the shared empty list node.</param>
        /// <param name="comparers">Receives the default comparers.</param>
        [RequiresDynamicCode("The verified constructors are called from DynamicMethods.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Missing private metadata leaves the constructors unbound.")]
        [UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "Missing private metadata leaves the constructors unbound.")]
        private static void Bind(out Func<object>? node, out WrapTree? wrap, out object? emptyTree, out object? emptyList, out object? comparers)
        {
            node = null;
            wrap = null;
            emptyTree = null;
            emptyList = null;
            comparers = null;
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var dictionary = typeof(ImmutableDictionary<TKey, TValue>);
            var empty = ImmutableDictionary<TKey, TValue>.Empty;
            var rootField = dictionary.GetField("_root", instance);
            var comparersField = dictionary.GetField("_comparers", instance);
            if (rootField is null || comparersField is null || rootField.FieldType.IsValueType || comparersField.FieldType.IsValueType)
                return;
            var tree = rootField.FieldType;
            var parameterless = tree.GetConstructor(instance, Type.EmptyTypes);
            var constructor = dictionary.GetConstructor(instance, [tree, comparersField.FieldType, typeof(int)]);
            var listNode = FieldType(FieldType(tree, "_value"), "_additionalElements");
            var listEmpty = listNode?.GetField("EmptyNode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (parameterless is null || constructor is null || listEmpty is null || listEmpty.FieldType != listNode)
                return;

            var allocate = new DynamicMethod("LuminImmutableDictionaryNode_" + typeof(TKey).Name + "_" + typeof(TValue).Name,
                typeof(object), Type.EmptyTypes, typeof(ImmutableDictionaryConstructors<TKey, TValue>).Module, true);
            var il = allocate.GetILGenerator();
            il.Emit(OpCodes.Newobj, parameterless);
            il.Emit(OpCodes.Ret);
            var create = new DynamicMethod("LuminImmutableDictionaryWrap_" + typeof(TKey).Name + "_" + typeof(TValue).Name,
                dictionary, [typeof(object), typeof(object), typeof(int)], typeof(ImmutableDictionaryConstructors<TKey, TValue>).Module, true);
            il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, tree);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, comparersField.FieldType);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Newobj, constructor);
            il.Emit(OpCodes.Ret);

            var allocator = allocate.CreateDelegate<Func<object>>();
            var wrapper = create.CreateDelegate<WrapTree>();
            object? emptyRoot = rootField.GetValue(empty);
            object? defaults = comparersField.GetValue(empty);
            object? list = listEmpty.GetValue(null);
            if (emptyRoot is null || defaults is null || list is null || allocator().GetType() != tree)
                return;
            // The constructor freezes the tree and keeps the comparers it receives; an empty one must equal the empty dictionary.
            var probe = wrapper(emptyRoot, defaults, 0);
            if (probe.GetType() != dictionary || probe.Count != 0 || !ReferenceEquals(rootField.GetValue(probe), emptyRoot) ||
                !ReferenceEquals(comparersField.GetValue(probe), defaults) || !ReferenceEquals(probe.KeyComparer, empty.KeyComparer) ||
                !ReferenceEquals(probe.ValueComparer, empty.ValueComparer))
                return;
            node = allocator;
            wrap = wrapper;
            emptyTree = emptyRoot;
            emptyList = list;
            comparers = defaults;
        }
    }
}
#endif

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
#if NET10_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
#endif

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Pushes a staged range as one detached chain with the framework's <c>PushRange</c> publication.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    /// <param name="stack">The nonnull destination stack.</param>
    /// <param name="items">The staged elements, bottom first.</param>
    /// <param name="count">The number of staged elements to push.</param>
    /// <remarks>On verified x64 .NET 10 and 11 layouts the chain is built by a JIT-compiled copy of the framework
    /// loop that calls the actual node constructor, links the nodes in the same order and publishes them with the same
    /// compare-exchange and private contention path. Other runtimes and layouts call <c>PushRange</c>.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void PushConcurrentStackRange<T>(ConcurrentStack<T> stack, T[] items, int count)
    {
#if NET10_0_OR_GREATER
        if (count > 0 && (uint)count <= (uint)items.Length && ConcurrentStackChain<T>.Factory is { } push)
        {
            push(stack, items, count);
            return;
        }
#endif
        stack.PushRange(items, 0, count);
    }

#if NET10_0_OR_GREATER
    /// <summary>Emits the framework's range push over its actual private node type.</summary>
    /// <typeparam name="T">The stored element type.</typeparam>
    private static class ConcurrentStackChain<T>
    {
        internal delegate void Push(ConcurrentStack<T> stack, T[] items, int count);
        internal static readonly Push? Factory = Probe();

        /// <summary>Compares emitted chains with framework range pushes before publishing the builder.</summary>
        /// <returns>The verified builder, or null on unsupported runtimes or layouts.</returns>
        private static Push? Probe()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version.Major is not (10 or 11) || !ConcurrentStackStorage<T>.Available)
                return null;
            try
            {
                var push = Create();
                if (push is null)
                    return null;
                var items = new T[17];
                foreach (int count in new[] { 1, 2, 3, 16, 17 })
                {
                    var expected = new ConcurrentStack<T>();
                    var actual = new ConcurrentStack<T>();
                    expected.PushRange(items, 0, count);
                    push(actual, items, count);
                    if (!SameChain(actual, expected) || actual.Count != count)
                        return null;
                    expected.PushRange(items, 0, count);
                    push(actual, items, count);
                    if (!SameChain(actual, expected) || actual.Count != 2 * count)
                        return null;
                }
                return push;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Checks the node types and chain length against the framework result.</summary>
        /// <param name="actual">The emitted publication.</param>
        /// <param name="expected">The framework publication.</param>
        /// <returns>Whether both chains have the same node types and length.</returns>
        private static bool SameChain(ConcurrentStack<T> actual, ConcurrentStack<T> expected)
        {
            object? left = LuminFieldLayout.Field<object?>(actual, ConcurrentStackStorage<T>.Head);
            object? right = LuminFieldLayout.Field<object?>(expected, ConcurrentStackStorage<T>.Head);
            while (left is not null && right is not null)
            {
                if (left.GetType() != right.GetType())
                    return false;
                left = LuminFieldLayout.Field<object?>(left, ConcurrentStackStorage<T>.Next);
                right = LuminFieldLayout.Field<object?>(right, ConcurrentStackStorage<T>.Next);
            }
            return left is null && right is null;
        }

        /// <summary>Binds the closed framework node and emits the range push loop.</summary>
        /// <returns>The builder, or null when a closed framework signature differs.</returns>
        [RequiresDynamicCode("The verified chain builder emits a DynamicMethod.")]
        private static Push? Create()
        {
            var stackType = typeof(ConcurrentStack<T>);
            const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var head = stackType.GetField("_head", Instance);
            var node = head?.FieldType;
            var next = node?.GetField("_next", Instance);
            var value = node?.GetField("_value", Instance);
            if (head is null || node is null || next is null || value is null || next.FieldType != node || value.FieldType != typeof(T))
                return null;
            var constructors = node.GetConstructors(Instance);
            var pushCore = stackType.GetMethod("PushCore", Instance, [node, node]);
            if (constructors.Length != 1 || constructors[0].GetParameters() is not [{ } parameter] || parameter.ParameterType != typeof(T) ||
                pushCore is null || pushCore.ReturnType != typeof(void))
                return null;
            var exchange = typeof(Interlocked).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(Interlocked.CompareExchange) && m.IsGenericMethodDefinition)
                .MakeGenericMethod(node);

            var method = new DynamicMethod("LuminConcurrentStackChain_" + typeof(T).Name, null,
                [stackType, typeof(T[]), typeof(int)], typeof(ConcurrentStackChain<T>).Module, true);
            var il = method.GetILGenerator();
            var first = il.DeclareLocal(node);
            var last = il.DeclareLocal(node);
            var current = il.DeclareLocal(node);
            var index = il.DeclareLocal(typeof(int));
            var expected = il.DeclareLocal(node);
            Label loop = il.DefineLabel(), condition = il.DefineLabel(), done = il.DefineLabel();

            // head = tail = new Node(items[0]);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldelem, typeof(T));
            il.Emit(OpCodes.Newobj, constructors[0]);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Stloc, first);
            il.Emit(OpCodes.Stloc, last);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Stloc, index);
            il.Emit(OpCodes.Br, condition);

            // node = new Node(items[i]); node._next = head; head = node;
            il.MarkLabel(loop);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldelem, typeof(T));
            il.Emit(OpCodes.Newobj, constructors[0]);
            il.Emit(OpCodes.Stloc, current);
            il.Emit(OpCodes.Ldloc, current);
            il.Emit(OpCodes.Ldloc, first);
            il.Emit(OpCodes.Stfld, next);
            il.Emit(OpCodes.Ldloc, current);
            il.Emit(OpCodes.Stloc, first);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, index);
            il.MarkLabel(condition);
            il.Emit(OpCodes.Ldloc, index);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Blt, loop);

            // tail._next = _head; if (CompareExchange(ref _head, head, tail._next) == tail._next) return; PushCore(head, tail);
            il.Emit(OpCodes.Ldloc, last);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Volatile);
            il.Emit(OpCodes.Ldfld, head);
            il.Emit(OpCodes.Stfld, next);
            il.Emit(OpCodes.Ldloc, last);
            il.Emit(OpCodes.Ldfld, next);
            il.Emit(OpCodes.Stloc, expected);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, head);
            il.Emit(OpCodes.Ldloc, first);
            il.Emit(OpCodes.Ldloc, expected);
            il.Emit(OpCodes.Call, exchange);
            il.Emit(OpCodes.Ldloc, expected);
            il.Emit(OpCodes.Beq, done);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, first);
            il.Emit(OpCodes.Ldloc, last);
            il.Emit(OpCodes.Call, pushCore);
            il.MarkLabel(done);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate<Push>();
        }
    }
#endif
}

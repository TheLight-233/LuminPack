#if NET10_0_OR_GREATER
using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Builds a queue from the completed prefix of a staging array.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    /// <param name="items">The staging array, which is neither changed nor retained.</param>
    /// <param name="count">The number of decoded elements.</param>
    /// <returns>A queue in the original element order.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ImmutableQueue<T> CreateImmutableQueue<T>(T[] items, int count)
    {
        if ((uint)count > (uint)items.Length)
            LuminPackExceptionHelper.ThrowSpanOutOfRange(count);
        if (count == 0)
            return ImmutableQueue<T>.Empty;
        if (ImmutableQueueConstructor<T>.Build is { } build)
            return build(items, count);
        return ImmutableQueue.Create(new ReadOnlySpan<T>(items, 0, count));
    }

    /// <summary>Binds one loop over the private stack and queue constructors for each closed element type.</summary>
    /// <typeparam name="T">The queue element type.</typeparam>
    private static class ImmutableQueueConstructor<T>
    {
        internal static readonly Func<T[], int, ImmutableQueue<T>>? Build;

        /// <summary>Attempts the optional binding where dynamic code is compiled.</summary>
        static ImmutableQueueConstructor()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
                return;
            try
            {
                Build = Bind();
            }
            catch
            {
                Build = null;
            }
        }

        /// <summary>Emits the reverse stack build and checks the resulting queue through its public operations.</summary>
        /// <returns>The bound constructor loop, or null when the framework does not match.</returns>
        [RequiresDynamicCode("The verified constructors are called from a DynamicMethod.")]
        private static Func<T[], int, ImmutableQueue<T>>? Bind()
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            Type stack = typeof(ImmutableStack<T>);
            Type queue = typeof(ImmutableQueue<T>);
            var push = stack.GetConstructor(privateInstance, [typeof(T), stack]);
            var wrap = queue.GetConstructor(privateInstance, [stack, stack]);
            var empty = stack.GetProperty(nameof(ImmutableStack<T>.Empty))?.GetMethod;
            if (push is null || wrap is null || empty is null)
                return null;

            var method = new DynamicMethod("LuminImmutableQueueBuild_" + typeof(T).Name, queue,
                [typeof(T[]), typeof(int)], typeof(ImmutableQueueConstructor<T>).Module, true);
            var il = method.GetILGenerator();
            il.DeclareLocal(stack);
            Label loop = il.DefineLabel(), test = il.DefineLabel();
            il.Emit(OpCodes.Call, empty);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Br_S, test);

            // stack = new ImmutableStack<T>(items[--count], stack);
            il.MarkLabel(loop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Starg_S, (byte)1);
            il.Emit(OpCodes.Ldelem, typeof(T));
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Newobj, push);
            il.Emit(OpCodes.Stloc_0);

            il.MarkLabel(test);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Brtrue_S, loop);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, empty);
            il.Emit(OpCodes.Newobj, wrap);
            il.Emit(OpCodes.Ret);

            var build = method.CreateDelegate<Func<T[], int, ImmutableQueue<T>>>();
            return Probe(build) ? build : null;
        }

        /// <summary>Checks the closed queue's publication and the integer constructor's element order.</summary>
        /// <param name="build">The private constructor loop.</param>
        /// <returns>Whether the queue accepts the constructed front chain.</returns>
        private static bool Probe(Func<T[], int, ImmutableQueue<T>> build)
        {
            T[] items = typeof(T) == typeof(int) ? (T[])(object)new[] { 2, 7, 11, 13 } : new T[4];
            var queue = build(items, 3);
            for (int index = 0; index < 3; index++)
            {
                if (queue.IsEmpty)
                    return false;
                if (typeof(T) == typeof(int))
                {
                    T item = queue.Peek();
                    if (Unsafe.As<T, int>(ref item) != Unsafe.As<T, int>(ref items[index]))
                        return false;
                }
                queue = queue.Dequeue();
            }
            return queue.IsEmpty;
        }
    }
}
#endif

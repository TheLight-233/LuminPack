using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static LuminPack.Code.LuminFieldLayout;

namespace LuminPack.Utility;

public static partial class LuminTreeStorage
{
#if NET10_0_OR_GREATER
    /// <summary>Emits the framework's red-black layout through its actual private node constructor.</summary>
    /// <typeparam name="T">The reference element of the verified closed framework set.</typeparam>
    private static class TypedSortedSetNodes<T>
    {
        internal delegate object? BuildTree(ref T first, int start, int end, object? carriedRed);
        internal static readonly BuildTree? Factory = Probe();

        /// <summary>Compares the emitted constructor with the existing bound framework constructor.</summary>
        /// <returns>The verified constructor, or null on unsupported runtimes or shapes.</returns>
        private static BuildTree? Probe()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
                Environment.Version.Major is not (10 or 11) || !SortedSetLayout<T>.Available)
                return null;
            try
            {
                var construct = Create();
                if (construct is null)
                    return null;
                var items = new T[33];
                var offsets = SortedSetLayout<T>.Nodes;
                foreach (int count in new[] { 1, 2, 3, 4, 6, 8, 15, 16, 17, 31, 32, 33 })
                {
                    object? expected = BclTrees.SortedSet<T>.Build(items, count);
                    object? actual = construct(ref MemoryMarshal.GetArrayDataReference(items), 0, count - 1, null);
                    if (!SameShape(actual, expected, in offsets))
                        return null;
                }
                return construct;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Checks type, color and child shape against the bound framework tree.</summary>
        /// <param name="actual">The proposed root.</param>
        /// <param name="expected">The reference root.</param>
        /// <param name="offsets">The node fields proved by the collection layout.</param>
        /// <returns>Whether the complete node shape is equivalent.</returns>
        private static bool SameShape(object? actual, object? expected, in NodeOffsets offsets) =>
            actual is null || expected is null ? ReferenceEquals(actual, expected) :
            actual.GetType() == expected.GetType() &&
            Field<byte>(actual, offsets.Color) == Field<byte>(expected, offsets.Color) &&
            SameShape(Field<object?>(actual, offsets.Left), Field<object?>(expected, offsets.Left), in offsets) &&
            SameShape(Field<object?>(actual, offsets.Right), Field<object?>(expected, offsets.Right), in offsets);

        /// <summary>Binds the closed framework node constructor and emits the existing sorted-array topology.</summary>
        /// <returns>The detached tree builder, or null when the closed framework signature differs.</returns>
        [RequiresDynamicCode("The verified node constructor emits a DynamicMethod.")]
        private static BuildTree? Create()
        {
            var set = typeof(SortedSet<T>);
            var node = set.GetField("root", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType;
            var constructor = node.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
            var parameters = constructor.GetParameters();
            var color = node.GetField("<Color>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var left = node.GetField("<Left>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var right = node.GetField("<Right>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(T) || parameters[1].ParameterType != color.FieldType ||
                !color.FieldType.IsEnum || Enum.GetUnderlyingType(color.FieldType) != typeof(byte) ||
                Convert.ToInt32(Enum.Parse(color.FieldType, "Black")) != 0 || Convert.ToInt32(Enum.Parse(color.FieldType, "Red")) != 1 ||
                left.FieldType != node || right.FieldType != node)
                return null;

            var add = typeof(Unsafe).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "Add" && m.IsGenericMethodDefinition && m.GetParameters()[0].ParameterType.IsByRef && m.GetParameters()[1].ParameterType == typeof(int))
                .MakeGenericMethod(typeof(T));
            var method = new DynamicMethod("LuminTypedSortedSetNodes_" + typeof(T).Name,
                typeof(object), [typeof(T).MakeByRefType(), typeof(int), typeof(int), typeof(object)], typeof(TypedSortedSetNodes<T>).Module, true);
            var il = method.GetILGenerator();
            var root = il.DeclareLocal(node);
            var size = il.DeclareLocal(typeof(int));
            var middle = il.DeclareLocal(typeof(int));
            Label zero = il.DefineLabel(), one = il.DefineLabel(), two = il.DefineLabel(), three = il.DefineLabel();
            Label odd = il.DefineLabel(), setRight = il.DefineLabel(), assignCarried = il.DefineLabel(), finish = il.DefineLabel();

            // General branch uses the same split, allocation order, and red node carried to the even right subtree.
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Stloc, size);
            il.Emit(OpCodes.Switch, new[] { zero, one, two, three });
            LoadMidpoint();
            il.Emit(OpCodes.Stloc, middle);
            New(() => il.Emit(OpCodes.Ldloc, middle), false);
            il.Emit(OpCodes.Stloc, root);
            il.Emit(OpCodes.Ldloc, root);
            Recur(() => il.Emit(OpCodes.Ldarg_1), () => ShiftLocal(middle, -1), () => il.Emit(OpCodes.Ldarg_3));
            il.Emit(OpCodes.Stfld, left);
            il.Emit(OpCodes.Ldloc, root);
            il.Emit(OpCodes.Ldloc, size);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.And);
            il.Emit(OpCodes.Brtrue, odd);
            Recur(() => ShiftLocal(middle, 2), () => il.Emit(OpCodes.Ldarg_2), () => New(() => ShiftLocal(middle, 1), true));
            il.Emit(OpCodes.Br, setRight);
            il.MarkLabel(odd);
            Recur(() => ShiftLocal(middle, 1), () => il.Emit(OpCodes.Ldarg_2), () => il.Emit(OpCodes.Ldnull));
            il.MarkLabel(setRight);
            il.Emit(OpCodes.Stfld, right);
            il.Emit(OpCodes.Br, finish);

            il.MarkLabel(zero);
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(one);
            New(() => il.Emit(OpCodes.Ldarg_1), false);
            il.Emit(OpCodes.Stloc, root);
            il.Emit(OpCodes.Br, assignCarried);

            il.MarkLabel(two);
            New(() => il.Emit(OpCodes.Ldarg_1), false);
            il.Emit(OpCodes.Stloc, root);
            il.Emit(OpCodes.Ldloc, root);
            New(() => il.Emit(OpCodes.Ldarg_2), true);
            il.Emit(OpCodes.Stfld, right);
            il.Emit(OpCodes.Br, assignCarried);

            il.MarkLabel(three);
            New(() => ShiftArgument(1, 1), false);
            il.Emit(OpCodes.Stloc, root);
            il.Emit(OpCodes.Ldloc, root);
            New(() => il.Emit(OpCodes.Ldarg_1), false);
            il.Emit(OpCodes.Stfld, left);
            il.Emit(OpCodes.Ldloc, root);
            New(() => il.Emit(OpCodes.Ldarg_2), false);
            il.Emit(OpCodes.Stfld, right);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Brfalse, finish);
            il.Emit(OpCodes.Ldloc, root);
            il.Emit(OpCodes.Ldfld, left);
            StoreCarriedLeft();
            il.Emit(OpCodes.Br, finish);

            il.MarkLabel(assignCarried);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Brfalse, finish);
            il.Emit(OpCodes.Ldloc, root);
            StoreCarriedLeft();
            il.MarkLabel(finish);
            il.Emit(OpCodes.Ldloc, root);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate<BuildTree>();

            void New(Action index, bool red)
            {
                il.Emit(OpCodes.Ldarg_0);
                index();
                il.Emit(OpCodes.Call, add);
                il.Emit(OpCodes.Ldind_Ref);
                il.Emit(red ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Newobj, constructor);
            }

            void Recur(Action start, Action end, Action red)
            {
                il.Emit(OpCodes.Ldarg_0);
                start();
                end();
                red();
                il.Emit(OpCodes.Call, method);
                il.Emit(OpCodes.Castclass, node);
            }

            void ShiftLocal(LocalBuilder local, int delta)
            {
                il.Emit(OpCodes.Ldloc, local);
                il.Emit(OpCodes.Ldc_I4, delta);
                il.Emit(OpCodes.Add);
            }
            void ShiftArgument(int arg, int delta)
            {
                il.Emit(OpCodes.Ldarg, (short)arg);
                il.Emit(OpCodes.Ldc_I4, delta);
                il.Emit(OpCodes.Add);
            }
            void LoadMidpoint()
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Shr);
            }
            void StoreCarriedLeft()
            {
                il.Emit(OpCodes.Ldarg_3);
                il.Emit(OpCodes.Castclass, node);
                il.Emit(OpCodes.Stfld, left);
            }
        }
    }
#endif
}

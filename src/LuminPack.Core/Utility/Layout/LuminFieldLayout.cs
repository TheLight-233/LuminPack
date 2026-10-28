using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

/// <summary>Locates instance fields of framework storage at run time and accesses them by byte offset.</summary>
/// <remarks>
/// <para>An offset is measured from the first byte of an object's instance data, the position of
/// <see cref="Data(object)"/>, which every runtime places directly after the object header.</para>
/// <para>A probe creates a zeroed instance with <see cref="RuntimeHelpers.GetUninitializedObject(Type)"/>, stores one field
/// whose bytes are all nonzero through reflection, and finds the first nonzero byte. Every other byte is zero, so the scan
/// stays inside the object and never reads a value as a reference. A layout whose metadata is unavailable, for example
/// after trimming, reports <see cref="Unavailable"/>.</para>
/// <para>Internally allocated probe objects have finalization suppressed because their constructors never ran.
/// Caller-supplied sentinel objects retain their original lifetime and finalization.</para>
/// <para>Stored as <see langword="static readonly"/> fields, the offsets are constants in optimized code, so an access is the
/// same single addressed load or store as a field of a declared type.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminFieldLayout
{
    /// <summary>The result of a probe that could not locate a field.</summary>
    public const int Unavailable = -1;

    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Gets the first byte of an object's instance data.</summary>
    /// <param name="instance">A non-null object.</param>
    /// <returns>A tracked reference to the object's first instance field position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref byte Data(object instance) => ref Unsafe.As<RawData>(instance).Data;

    /// <summary>Gets an instance field of an object at a probed offset.</summary>
    /// <typeparam name="TField">The field's storage type, or a type with the same storage for a field whose type cannot be named.</typeparam>
    /// <param name="instance">A non-null object of the probed type.</param>
    /// <param name="offset">A probed, available offset.</param>
    /// <returns>A tracked reference to the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TField Field<TField>(object instance, int offset)
        => ref Unsafe.As<byte, TField>(ref Unsafe.Add(ref Unsafe.As<RawData>(instance).Data, (nint)(uint)offset));

    /// <summary>Gets a field of a value stored at a reference, such as an array element.</summary>
    /// <typeparam name="TField">The field's storage type.</typeparam>
    /// <param name="storage">The first byte of the value.</param>
    /// <param name="offset">A probed, available offset within the value.</param>
    /// <returns>A tracked reference to the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref TField Field<TField>(ref byte storage, int offset)
        => ref Unsafe.As<byte, TField>(ref Unsafe.Add(ref storage, (nint)(uint)offset));

    /// <summary>Selects a probed offset, or the offset of the compiled view when the probe was unavailable.</summary>
    /// <param name="probed">The probed offset.</param>
    /// <param name="compiled">The offset of the same field in the compiled view of the expected layout.</param>
    /// <returns>The offset to use.</returns>
    public static int Prefer(int probed, int compiled) => probed >= 0 ? probed : compiled;

    /// <summary>Measures the offset of a field of a compiled view type, used when metadata cannot be probed.</summary>
    /// <param name="instance">An instance of the view type.</param>
    /// <param name="field">The view field.</param>
    /// <returns>The field's offset from the instance data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OffsetOf(object instance, ref byte field) => (int)Unsafe.ByteOffset(ref Data(instance), ref field);

    /// <summary>Finds the offset of an instance field of a class.</summary>
    /// <param name="owner">The closed class type; inherited fields are found as well.</param>
    /// <param name="name">The field name.</param>
    /// <param name="sentinel">A value assignable to a reference field whose type has no constructible implementation.</param>
    /// <returns>The offset from the instance data, or <see cref="Unavailable"/>.</returns>
    public static int ObjectField(Type? owner, string name, object? sentinel = null)
    {
        try
        {
            if (owner is null || owner.IsValueType || owner.IsAbstract || owner.ContainsGenericParameters) return Unavailable;
            var field = Find(owner, name);
            if (field is null) return Unavailable;
            object instance = CreateProbeObject(owner);
            return Locate(field, instance, ref Data(instance), sentinel);
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>Finds the offset of an instance field of a struct within the struct's storage.</summary>
    /// <param name="owner">The closed struct type.</param>
    /// <param name="name">The field name.</param>
    /// <param name="sentinel">A value assignable to a reference field whose type has no constructible implementation.</param>
    /// <returns>The offset from the first byte of the struct, or <see cref="Unavailable"/>.</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "This optional probe catches unavailable constructor metadata and returns Unavailable; it does not require preserving the inspected type.")]
#endif
    public static int ValueField(Type? owner, string name, object? sentinel = null)
    {
        try
        {
            if (owner is null || !owner.IsValueType || owner.ContainsGenericParameters) return Unavailable;
            var field = Find(owner, name);
            if (field is null) return Unavailable;
            // A boxed struct stores the struct's bytes as its instance data.
            object box = RuntimeHelpers.GetUninitializedObject(owner);
            return Locate(field, box, ref Data(box), sentinel);
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>Finds the distance between consecutive elements of an array of a type.</summary>
    /// <param name="element">The closed element type.</param>
    /// <returns>The element stride in bytes, or <see cref="Unavailable"/>.</returns>
    public static int ArrayStride(Type? element)
    {
        try
        {
            if (element is null || element.ContainsGenericParameters) return Unavailable;
            if (!element.IsValueType) return IntPtr.Size;
            if (!TryFill(element, null, out object? value)) return Unavailable;
            var array = Array.CreateInstance(element, 2);
            array.SetValue(value, 1);
            // Every single-dimensional array stores its elements after the same header, so a byte view finds element zero.
            ref byte first = ref Unsafe.As<byte[]>(array)[0];
            return FirstNonZero(ref first) - FirstNonZero(ref Data(value!));
        }
        catch
        {
            return Unavailable;
        }
    }

    /// <summary>Gets a declared field of a type or of one of its base types.</summary>
    /// <param name="owner">The searched type.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field, or null.</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "This optional lookup intentionally permits absent trimmed fields; callers reject missing metadata or use their established layout fallback.")]
#endif
    public static FieldInfo? Find(Type? owner, string name)
    {
        for (var type = owner; type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, InstanceFields);
            if (field is not null) return field;
        }
        return null;
    }

    /// <summary>Gets the type of a field, from which nested private storage types are reached without constructing generics.</summary>
    /// <param name="owner">The searched type.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field type, or null.</returns>
    public static Type? FieldType(Type? owner, string name)
    {
        try { return Find(owner, name)?.FieldType; }
        catch { return null; }
    }

    /// <summary>Stores a field with nonzero bytes in a zeroed instance and measures its position.</summary>
    /// <param name="field">The field to store.</param>
    /// <param name="instance">The zeroed instance.</param>
    /// <param name="data">The first byte of the inspected storage.</param>
    /// <param name="sentinel">A value assignable to a reference field without a constructible implementation, or null.</param>
    /// <returns>The result.</returns>
    private static int Locate(FieldInfo field, object instance, ref byte data, object? sentinel)
    {
        var type = field.FieldType;
        if (!type.IsValueType)
        {
            object? reference = sentinel ?? CreateReference(type);
            if (reference is null) return Unavailable;
            field.SetValue(instance, reference);
            // References are pointer aligned and every slot before the field is null.
            for (int offset = 0; ; offset += IntPtr.Size)
                if (Unsafe.As<byte, nint>(ref Unsafe.Add(ref data, unchecked((nint)(uint)(offset)))) != 0) return offset;
        }
        if (!TryFill(type, sentinel, out object? value)) return Unavailable;
        field.SetValue(instance, value);
        return FirstNonZero(ref data) - FirstNonZero(ref Data(value!));
    }

    /// <summary>Finds the first nonzero byte, which a filled value guarantees within its storage.</summary>
    /// <param name="data">The first byte of the inspected storage.</param>
    /// <returns>The result.</returns>
    private static int FirstNonZero(ref byte data)
    {
        for (int offset = 0; ; offset++)
            if (Unsafe.Add(ref data, unchecked((nint)(uint)(offset))) != 0) return offset;
    }

    /// <summary>Creates a boxed value of a type whose fields have nonzero bytes.</summary>
    /// <param name="type">The inspected type.</param>
    /// <param name="sentinel">A value assignable to a reference field without a constructible implementation, or null.</param>
    /// <param name="value">The value.</param>
    /// <returns>Whether the operation used the located storage.</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "The enclosing optional layout probe catches unavailable constructor metadata and reports Unavailable.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Missing trimmed fields are intentional: an unfilled value rejects the optional probe instead of requiring reflection metadata preservation.")]
#endif
    private static bool TryFill(Type type, object? sentinel, out object? value)
    {
        value = null;
        if (!BitConverter.IsLittleEndian) return false;
        if (type.IsEnum)
        {
            if (!TryFill(Enum.GetUnderlyingType(type), null, out var underlying)) return false;
            value = Enum.ToObject(type, underlying!);
            return true;
        }
        if (type.IsPrimitive)
        {
            value = Type.GetTypeCode(type) switch
            {
                TypeCode.Boolean => true,
                TypeCode.Char => (char)0x5A5A,
                TypeCode.SByte => (sbyte)0x5A,
                TypeCode.Byte => (byte)0x5A,
                TypeCode.Int16 => (short)0x5A5A,
                TypeCode.UInt16 => (ushort)0x5A5A,
                TypeCode.Int32 => 0x5A5A5A5A,
                TypeCode.UInt32 => 0x5A5A5A5Au,
                TypeCode.Int64 => 0x5A5A5A5A5A5A5A5AL,
                TypeCode.UInt64 => 0x5A5A5A5A5A5A5A5AUL,
                TypeCode.Single => 3.4063e16f,
                TypeCode.Double => 1.2e127,
                _ => type == typeof(IntPtr) ? (object)(IntPtr)0x5A5A5A5A : type == typeof(UIntPtr) ? (UIntPtr)0x5A5A5A5Au : null
            };
            return value is not null;
        }
        if (!type.IsValueType) return false;
        object box = RuntimeHelpers.GetUninitializedObject(type);
        bool filled = false;
        foreach (var field in type.GetFields(InstanceFields))
        {
            object? member;
            if (field.FieldType.IsValueType)
            {
                if (!TryFill(field.FieldType, null, out member)) continue;
            }
            else if ((member = (sentinel is not null && field.FieldType.IsInstanceOfType(sentinel) ? sentinel : CreateReference(field.FieldType))) is null)
                continue;
            field.SetValue(box, member);
            filled = true;
        }
        value = box;
        return filled;
    }

    /// <summary>Creates an object assignable to a reference field.</summary>
    /// <param name="type">The inspected type.</param>
    /// <returns>The result.</returns>
    private static object? CreateReference(Type type)
    {
        if (type == typeof(string)) return "\u5A5A";
        if (type == typeof(object)) return new object();
        if (type.IsArray) return type.GetArrayRank() == 1 ? Array.CreateInstance(type.GetElementType()!, 0) : null;
        if (type.ContainsGenericParameters || typeof(Delegate).IsAssignableFrom(type)) return null;
        if (!type.IsInterface && !type.IsAbstract) return CreateProbeObject(type);
        // Comparer and list fields of the probed collections are declared as interfaces; a framework instance stands in.
        foreach (var candidate in s_interfaceSentinels)
            if (type.IsInstanceOfType(candidate)) return candidate;
        return null;
    }

    /// <summary>Allocates temporary storage without scheduling a finalizer for an unconstructed object.</summary>
    /// <param name="type">The concrete reference type used only by the layout probe.</param>
    /// <returns>The zeroed temporary object, with its finalizer suppressed.</returns>
#if NET5_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "The enclosing optional layout probe catches unavailable constructor metadata and reports Unavailable; no production object is created through this helper.")]
#endif
    private static object CreateProbeObject(Type type)
    {
        object value = RuntimeHelpers.GetUninitializedObject(type);
        GC.SuppressFinalize(value);
        return value;
    }

    private static readonly object[] s_interfaceSentinels =
    {
        EqualityComparer<int>.Default, EqualityComparer<object>.Default, Comparer<int>.Default, Comparer<object>.Default,
        new List<int>(), new List<object>(), new System.Collections.ObjectModel.ObservableCollection<int>(),
        new System.Collections.ObjectModel.ObservableCollection<object>()
    };

    /// <summary>Reinterprets an object to reach its first instance data byte.</summary>
    private sealed class RawData
    {
        public byte Data;
    }
}

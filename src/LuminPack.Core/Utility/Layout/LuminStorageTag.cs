using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPack.Utility;

/// <summary>Describes the generated storage layout models of a struct whose native layout can differ between processes.</summary>
/// <remarks>
/// Model numbers start at one and are fixed by the generator, so a tag written by one process names the same offsets in every
/// other process. Each model lists, for every packed data byte, the offset of that byte in a native element of that model.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ILuminStorageLayout : ILuminPortableLayout
{
    /// <summary>Gets the number of generated models; their tags are one through this count.</summary>
    int ModelCount { get; }

    /// <summary>Gets the width of one native element in a model.</summary>
    /// <param name="tag">The model tag.</param>
    /// <returns>The element width, including padding.</returns>
    int ModelSize(int tag);

    /// <summary>Writes, for each packed data byte, the offset of the byte in a native element of a model.</summary>
    /// <param name="tag">The model tag.</param>
    /// <param name="storageOffsets">One entry per packed data byte.</param>
    void DescribeModel(int tag, Span<int> storageOffsets);
}

/// <summary>Writes and reads native struct storage under a one-byte layout tag.</summary>
/// <typeparam name="T">The unmanaged struct, including a constructed Nullable.</typeparam>
/// <typeparam name="TLayout">The generated layout models of <typeparamref name="T"/>.</typeparam>
/// <remarks>
/// <para>
/// The wire carries a tag naming the model whose element offsets the following bytes use. When the running process lays
/// the struct out exactly like a model, that model's tag is written and the elements are copied as one block. Otherwise the
/// first model is the canonical form and elements are converted on write. A reader whose own layout equals the tag's model
/// copies a block; any other reader converts between the two offset lists with <see cref="LuminLayout"/>, which uses
/// byte shuffles where the instruction set has them. Offsets are verified against the running process's field addresses once
/// per closed type, so a wrong model prediction costs speed, never correctness.
/// </para>
/// <para>A struct whose layout is identical in every model is never tagged by the generator and keeps its headerless native copy.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminStorageTag<T, TLayout>
    where TLayout : struct, ILuminStorageLayout
{
    /// <summary>The tag this process writes; the model whose offsets equal the running layout, or the canonical first model.</summary>
    public static readonly byte Tag;

    /// <summary>The wire width of one element for <see cref="Tag"/>.</summary>
    public static readonly int Width;

    /// <summary>The widest model, an upper bound for any tagged element.</summary>
    public static readonly int MaxWidth;

    /// <summary>The packed data width of one element, a lower bound for any tagged element.</summary>
    public static readonly int MinWidth;

    private static readonly bool Native;
    private static readonly int LocalSize;
    private static readonly int[] ModelWidths;
    private static readonly LuminLayout[] FromModel;
    private static readonly LuminLayout ToCanonical;

    /// <summary>Verifies the models against the running layout once per closed type.</summary>
    static LuminStorageTag()
    {
        var layout = default(TLayout);
        int size = Unsafe.SizeOf<T>(), data = layout.WireSize, models = layout.ModelCount;
        if (models < 1 || models > 250 || data <= 0)
            LuminPackExceptionHelper.ThrowInvalidDataException("The generated storage layout of " + typeof(T) + " is invalid.");
        var local = new int[data];
        layout.Describe(local);
        var offsets = new int[models + 1][];
        ModelWidths = new int[models + 1];
        FromModel = new LuminLayout[models + 1];
        byte tag = 0;
        int widest = 0, narrowest = int.MaxValue;
        for (int model = 1; model <= models; model++)
        {
            offsets[model] = new int[data];
            layout.DescribeModel(model, offsets[model]);
            ModelWidths[model] = layout.ModelSize(model);
            widest = Math.Max(widest, ModelWidths[model]);
            narrowest = Math.Min(narrowest, ModelWidths[model]);
            FromModel[model] = new LuminLayout(offsets[model], ModelWidths[model], local, size);
            if (tag == 0 && ModelWidths[model] == size && offsets[model].AsSpan().SequenceEqual(local))
                tag = (byte)model;
        }
        Native = tag != 0;
        Tag = Native ? tag : (byte)1;
        Width = ModelWidths[Tag];
        MaxWidth = widest;
        MinWidth = narrowest;
        LocalSize = size;
        ToCanonical = Native ? null : new LuminLayout(local, size, offsets[Tag], Width);
    }

    /// <summary>Gets the element width of a tag read from untrusted input.</summary>
    /// <param name="tag">The tag read from the wire.</param>
    /// <returns>The width of one element in that model.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WidthOf(byte tag)
    {
        if (tag == Tag)
            return Width;
        return WidthOfSlow(tag);
    }

    /// <summary>Rejects an unknown tag or returns the width of another process's model.</summary>
    /// <param name="tag">The tag read from the wire.</param>
    /// <returns>The width of one element in that model.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WidthOfSlow(byte tag)
    {
        if (tag == 0 || tag >= ModelWidths.Length)
            LuminPackExceptionHelper.ThrowInvalidDataException("Unknown storage layout tag " + tag + " for " + typeof(T) + ".");
        return ModelWidths[tag];
    }

    /// <summary>Writes elements in the model named by <see cref="Tag"/>.</summary>
    /// <param name="first">The first element.</param>
    /// <param name="count">The element count; the caller has reserved <c>count * Width</c> bytes.</param>
    /// <param name="destination">The first destination byte.</param>
    public static void Pack(ref T first, int count, ref byte destination)
    {
        if (count == 0)
            return;
        ref byte source = ref Unsafe.As<T, byte>(ref first);
        if (Native)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, checked((uint)count * (uint)LocalSize));
            return;
        }
        // Padding in the canonical form is zeroed so the output does not depend on the buffer's previous contents.
        Unsafe.InitBlockUnaligned(ref destination, 0, checked((uint)count * (uint)Width));
        ToCanonical.Convert(ref source, ref destination, count);
    }

    /// <summary>Reads elements written in the model named by a tag.</summary>
    /// <param name="tag">The validated wire tag.</param>
    /// <param name="source">The first source byte.</param>
    /// <param name="count">The element count; the caller has validated <c>count * WidthOf(tag)</c> readable bytes.</param>
    /// <param name="first">The first destination element.</param>
    public static void Unpack(byte tag, ref byte source, int count, ref T first)
    {
        if (count == 0)
            return;
        ref byte destination = ref Unsafe.As<T, byte>(ref first);
        if (tag == Tag && Native)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, checked((uint)count * (uint)LocalSize));
            return;
        }
        // Local padding keeps its previous content: only data bytes are meaningful to either side.
        WidthOf(tag);
        FromModel[tag].Convert(ref source, ref destination, count);
    }
}

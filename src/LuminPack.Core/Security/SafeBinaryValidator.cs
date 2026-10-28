using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Security;

/// <summary>Validates the original fixed, packed, object and collection contracts without constructing application objects.</summary>
internal static unsafe class SafeBinaryValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>Checks a complete immutable snapshot with one shared root budget.</summary>
    /// <param name="payload">The independently owned original-format payload.</param>
    /// <param name="schema">The compiler-proven closed graph.</param>
    /// <param name="budget">The root's cumulative resource budget.</param>
    /// <param name="node">The independently bounded value node.</param>
    internal static void Validate(ReadOnlySpan<byte> payload, LuminPackSafeSchema schema, SafeReadBudget budget, int node = 0)
    {
        fixed (byte* pinned = payload)
        {
            var cursor = new Cursor(new ReadOnlySpan<byte>(pinned, payload.Length), schema, budget);
            cursor.Read(node, 1, false);
            if (cursor.Position != payload.Length) LuminPackInputPolicy.Invalid("The schema did not consume the complete binary payload.");
        }
    }

    /// <summary>Owns checked cursor arithmetic independently of the original decoder's target publication.</summary>
    private ref struct Cursor
    {
        private readonly ReadOnlySpan<byte> _input;
        private readonly LuminPackSafeSchema _schema;
        private readonly SafeReadBudget _budget;
        internal int Position;

        /// <summary>Creates a parser over the pinned root snapshot.</summary>
        /// <param name="input">The pinned complete payload.</param>
        /// <param name="schema">The reviewed graph.</param>
        /// <param name="budget">The cumulative root budget.</param>
        internal Cursor(ReadOnlySpan<byte> input, LuminPackSafeSchema schema, SafeReadBudget budget)
        { _input = input; _schema = schema; _budget = budget; Position = 0; }

        /// <summary>Checks and consumes a complete range before reading its contents.</summary>
        /// <param name="bytes">The nonnegative range width.</param>
        /// <returns>The bounded range.</returns>
        private ReadOnlySpan<byte> Take(int bytes)
        {
            if ((uint)bytes > (uint)(_input.Length - Position)) LuminPackInputPolicy.Invalid("The binary payload is truncated.");
            _budget.Work(bytes);
            var result = _input.Slice(Position, bytes);
            Position += bytes;
            return result;
        }

        /// <summary>Reads a checked little-endian collection count.</summary>
        /// <returns>The count or null marker.</returns>
        private int Count()
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Take(4));
            if (count < -1) LuminPackInputPolicy.Invalid("A collection count is negative.");
            return count;
        }

        /// <summary>Validates one value and optionally returns its framework-only key representation.</summary>
        /// <param name="index">The closed node index.</param>
        /// <param name="depth">The current graph depth.</param>
        /// <param name="key">Whether a duplicate-check key is required.</param>
        /// <returns>A comparer-safe primitive key, or the default value for other payloads.</returns>
        internal SafeInputKey Read(int index, int depth, bool key)
        {
            _budget.Depth(depth);
            var node = _schema.Nodes[index];
            var kind = node.Kind;
            switch (kind)
            {
                case LuminPackSafeKind.String: return String(node.Mode, key);
                case LuminPackSafeKind.Struct:
                    foreach (int child in node.Children) Read(child, depth + 1, false);
                    return default;
                case LuminPackSafeKind.Object:
                    byte marker = Take(1)[0];
                    if (marker == 255 && node.Rank == 0) return default;
                    if (marker != node.Children.Length) LuminPackInputPolicy.Invalid("The object member header disagrees with its schema.");
                    if (node.Rank == 0) { _budget.Object(); _budget.Allocate(node.Storage + 64L); }
                    foreach (int child in node.Children) Read(child, depth + 1, false);
                    return default;
                case LuminPackSafeKind.Nullable:
                    byte present = Take(1)[0];
                    if (present == 255) return default;
                    if (present != 0) LuminPackInputPolicy.Invalid("The nullable marker is invalid.");
                    return Read(node.Children[0], depth + 1, key);
                case LuminPackSafeKind.Sequence:
                case LuminPackSafeKind.Set:
                case LuminPackSafeKind.Dictionary:
                case LuminPackSafeKind.Queue:
                case LuminPackSafeKind.MultiArray:
                    Collection(node, depth);
                    return default;
                case LuminPackSafeKind.Unsupported:
                    LuminPackInputPolicy.Invalid("The input uses an unreviewed wire shape.");
                    return default;
            }
            return Scalar(node, key);
        }

        /// <summary>Checks framing, actual storage costs, uniqueness and all children before any target container exists.</summary>
        /// <param name="node">The collection contract.</param>
        /// <param name="depth">The current depth.</param>
        private void Collection(LuminPackSafeNode node, int depth)
        {
            long dimensions = 1;
            if (node.Kind == LuminPackSafeKind.MultiArray)
            {
                byte marker = Take(1)[0];
                if (marker == 255) return;
                if (marker != 0) LuminPackInputPolicy.Invalid("The array object marker is invalid.");
                bool empty = false;
                for (int i = 0; i < node.Rank; i++)
                {
                    int size = Count();
                    if (size < 0 || size > _budget.Limits.MaxCollectionLength) SafeReadBudget.Fail("array dimension");
                    if (size == 0) empty = true;
                    if (!empty)
                    {
                        dimensions *= size;
                        if (dimensions > _budget.Limits.MaxCollectionLength) SafeReadBudget.Fail("array element count");
                    }
                }
                if (empty) dimensions = 0;
            }
            int count = Count();
            if (count == -1)
            {
                if (node.Kind == LuminPackSafeKind.MultiArray) LuminPackInputPolicy.Invalid("A present array cannot have a null payload.");
                return;
            }
            if (node.Kind == LuminPackSafeKind.MultiArray && count != dimensions) LuminPackInputPolicy.Invalid("Array dimensions disagree with the element count.");
            int storage = 0;
            foreach (int child in node.Children) storage = checked(storage + _schema.Nodes[child].Storage);
            _budget.Collection(count, storage);
            if (count != 0 && node.Kind == LuminPackSafeKind.Queue)
                if (Count() != 0 || Count() != 0 || Count() != count) LuminPackInputPolicy.Invalid("The canonical queue framing is invalid.");
            bool unique = node.Kind is LuminPackSafeKind.Set or LuminPackSafeKind.Dictionary;
            var element = _schema.Nodes[node.Children[0]];
            if (!unique && element.Kind is LuminPackSafeKind.Byte or LuminPackSafeKind.SByte or
                LuminPackSafeKind.Int16 or LuminPackSafeKind.UInt16 or LuminPackSafeKind.Int32 or LuminPackSafeKind.UInt32 or
                LuminPackSafeKind.Int64 or LuminPackSafeKind.UInt64 or LuminPackSafeKind.Single or LuminPackSafeKind.Double or
                LuminPackSafeKind.Char or LuminPackSafeKind.Guid or LuminPackSafeKind.TimeSpan)
            {
                // Every bit pattern is valid for these fixed-width elements. Validate their complete range once.
                if (count != 0) _budget.Depth(depth + 1);
                Take(checked(count * element.Storage));
                return;
            }
            // Bound the original container's worst collision chain as well as this validator's own table.
            if (unique) _budget.Work((long)count * count);
            var keys = unique ? new HashSet<SafeInputKey>(new SafeInputKeyComparer(_budget)) : null;
            for (int i = 0; i < count; i++)
            {
                var item = Read(node.Children[0], depth + 1, unique);
                if (unique)
                {
                    if (item.Text != null) _budget.Work(2L * count * item.Text.Length);
                    if (node.Kind == LuminPackSafeKind.Dictionary && item.IsNull) LuminPackInputPolicy.Invalid("A dictionary key is null.");
                    if (!keys!.Add(item)) LuminPackInputPolicy.Invalid("A set or dictionary contains duplicate keys or elements.");
                }
                if (node.Kind == LuminPackSafeKind.Dictionary) Read(node.Children[1], depth + 1, false);
            }
        }

        /// <summary>Checks scalar domains using bounded byte reads rather than architecture-sensitive aggregate loads.</summary>
        /// <param name="node">The scalar contract.</param>
        /// <param name="key">Whether a key must be returned.</param>
        /// <returns>The canonical framework key.</returns>
        private SafeInputKey Scalar(LuminPackSafeNode node, bool key)
        {
            int width = node.Kind == LuminPackSafeKind.Enum ? node.Rank : node.Storage;
            var bytes = Take(width);
            ulong first = width switch { 1 => bytes[0], 2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes), 8 or 16 => ReadWide(bytes), _ => 0 };
            ulong second = width == 16 ? ReadWide(bytes.Slice(8)) : 0;
            if (node.Kind == LuminPackSafeKind.Boolean && first > 1) LuminPackInputPolicy.Invalid("A Boolean has a noncanonical storage value.");
            if (node.Kind == LuminPackSafeKind.Enum) ValidateEnum(node, first, _budget);
            if (node.Kind == LuminPackSafeKind.DateTime && (first & 0x3FFFFFFFFFFFFFFFUL) > 3155378975999999999UL)
                LuminPackInputPolicy.Invalid("A DateTime tick value is outside its domain.");
            if (node.Kind == LuminPackSafeKind.Decimal)
            {
                decimal value = default;
                LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref System.Runtime.CompilerServices.Unsafe.As<decimal, byte>(ref value),
                    ref MemoryMarshal.GetReference(bytes), 16);
                _budget.Allocate(64);
                int flags = decimal.GetBits(value)[3];
                if ((flags & 0x7F00FFFF) != 0 || ((flags >> 16) & 255) > 28) LuminPackInputPolicy.Invalid("A Decimal has invalid scale or reserved bits.");
            }
            if (!key) return default;
            return SafeInputKey.Number(node.Kind, first, second);
        }

        /// <summary>Uses two individually supported four-byte loads on 32-bit IL2CPP targets.</summary>
        /// <param name="bytes">An already validated complete eight-byte range.</param>
        /// <returns>The little-endian scalar bits.</returns>
        private static ulong ReadWide(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes) |
            ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4)) << 32);

        /// <summary>Checks a string's complete framing, Unicode, character budget and final allocation estimate.</summary>
        /// <param name="mode">The existing finite string mode.</param>
        /// <param name="key">Whether an ordinal string key is needed.</param>
        /// <returns>The decoded key only when uniqueness checks require one.</returns>
        private SafeInputKey String(int mode, bool key)
        {
            int start = Position;
            ReadOnlySpan<byte> text;
            int characters;
            bool latin1 = false;
            if ((mode & 1) != 0)
            {
                int header = BinaryPrimitives.ReadInt32LittleEndian(Take(4));
                int recorded = mode == 1 ? BinaryPrimitives.ReadInt32LittleEndian(Take(4)) : 0;
                if (header == -1) return new SafeInputKey(LuminPackSafeKind.String, 0, 0, null);
                if (header < 0 || recorded < 0) LuminPackInputPolicy.Invalid("A string length is negative.");
                if (mode == 3)
                {
                    latin1 = (header & 1) != 0;
                    characters = header >> 1;
                    if (characters > _budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
                    text = Take(latin1 ? characters : header);
                }
                else
                {
                    if (recorded > _budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
                    text = Take(header);
                    characters = StrictUtf8.GetCharCount(text);
                    if (characters != recorded) LuminPackInputPolicy.Invalid("UTF-8 byte and character counts disagree.");
                }
            }
            else
            {
                int width = mode == 0 ? 1 : 2;
                int end = Position;
                long maximum = _budget.Limits.MaxStringLength * (mode == 0 ? 4L : 2L);
                while (true)
                {
                    if (_input.Length - end < width) LuminPackInputPolicy.Invalid("A string terminator is missing.");
                    if (_input[end] == 0 && (width == 1 || _input[end + 1] == 0)) break;
                    end += width;
                    if (end - Position > maximum) SafeReadBudget.Fail("string length");
                }
                text = Take(end - Position);
                Take(width);
                characters = mode == 0 ? StrictUtf8.GetCharCount(text) : text.Length / 2;
            }
            if (characters > _budget.Limits.MaxStringLength) SafeReadBudget.Fail("string length");
            _budget.Work(text.Length);
            if (mode >= 2 && !latin1) ValidateUtf16(text);
            if (characters != 0) { _budget.Object(); _budget.Allocate(characters * 4L + 128); }
            if (!key) return default;
            var span = _input.Slice(start, Position - start);
            var reader = new LuminPackReader(ref span);
            string? value = mode switch
            {
                0 => reader.Read<LuminPackUtf8TokenOptions>((string?)null),
                1 => reader.Read<LuminPackUtf8LengthOptions>((string?)null),
                2 => reader.Read<LuminPackUtf16TokenOptions>((string?)null),
                _ => reader.Read<LuminPackUtf16LengthOptions>((string?)null)
            };
            return new SafeInputKey(LuminPackSafeKind.String, 0, 0, value);
        }
    }

    /// <summary>Validates canonical UTF-16 without creating a string or reading an incomplete code unit.</summary>
    /// <param name="bytes">The complete little-endian code units.</param>
    internal static void ValidateUtf16(ReadOnlySpan<byte> bytes)
    {
        if ((bytes.Length & 1) != 0) LuminPackInputPolicy.Invalid("A UTF-16 payload has an incomplete code unit.");
        for (int i = 0; i < bytes.Length; i += 2)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i, 2));
            if (value is >= 0xD800 and <= 0xDBFF)
            {
                i += 2;
                if (i >= bytes.Length) LuminPackInputPolicy.Invalid("A UTF-16 high surrogate has no low surrogate.");
                ushort next = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i, 2));
                if (next is < 0xDC00 or > 0xDFFF) LuminPackInputPolicy.Invalid("A UTF-16 surrogate pair is invalid.");
            }
            else if (value is >= 0xDC00 and <= 0xDFFF) LuminPackInputPolicy.Invalid("A UTF-16 low surrogate has no high surrogate.");
        }
    }

    /// <summary>Rejects unknown enum values or flag bits before the original unsafe scalar read.</summary>
    /// <param name="node">The compiler-provided enum domain.</param>
    /// <param name="value">The bounded bit pattern.</param>
    /// <param name="budget">The shared comparison budget.</param>
    internal static void ValidateEnum(LuminPackSafeNode node, ulong value, SafeReadBudget budget)
    {
        budget.Work(node.Values.Length + 1L);
        ulong mask = 0;
        foreach (ulong allowed in node.Values)
        {
            if (value == allowed) return;
            mask |= allowed;
        }
        if (node.Flags && (value & ~mask) == 0) return;
        LuminPackInputPolicy.Invalid("An enum value is outside its declared domain.");
    }
}

/// <summary>Contains only reviewed framework scalar data; no input-selected comparer or application equality code is invoked.</summary>
internal readonly struct SafeInputKey
{
    internal readonly LuminPackSafeKind Kind;
    internal readonly ulong First;
    internal readonly ulong Second;
    internal readonly string? Text;
    internal bool IsNull => Kind == LuminPackSafeKind.String && Text is null;

    /// <summary>Captures a canonical framework key.</summary>
    /// <param name="kind">The primitive kind.</param>
    /// <param name="first">The low scalar bits.</param>
    /// <param name="second">The high scalar bits.</param>
    /// <param name="text">Decoded ordinal text, or null.</param>
    internal SafeInputKey(LuminPackSafeKind kind, ulong first, ulong second, string? text)
    { Kind = kind; First = first; Second = second; Text = text; }

    /// <summary>Normalizes signed zero and all NaNs according to framework floating-point equality.</summary>
    /// <param name="kind">The scalar kind.</param>
    /// <param name="first">The low bits.</param>
    /// <param name="second">The high bits.</param>
    /// <returns>The equality-canonical key.</returns>
    internal static SafeInputKey Number(LuminPackSafeKind kind, ulong first, ulong second = 0)
    {
        if (kind == LuminPackSafeKind.Single)
        {
            if ((first & 0x7FFFFFFF) == 0) first = 0;
            else if ((first & 0x7F800000) == 0x7F800000 && (first & 0x7FFFFF) != 0) first = 0x7FC00000;
        }
        if (kind == LuminPackSafeKind.Double)
        {
            if ((first & 0x7FFFFFFFFFFFFFFFUL) == 0) first = 0;
            else if ((first & 0x7FF0000000000000UL) == 0x7FF0000000000000UL && (first & 0xFFFFFFFFFFFFFUL) != 0) first = 0x7FF8000000000000UL;
        }
        return new SafeInputKey(kind, first, second, null);
    }
}

/// <summary>Bounds collision comparisons while retaining exact scalar and ordinal-string uniqueness.</summary>
internal sealed class SafeInputKeyComparer : IEqualityComparer<SafeInputKey>
{
    private readonly SafeReadBudget _budget;
    /// <summary>Creates a comparison view of the shared root budget.</summary>
    /// <param name="budget">The cumulative work budget.</param>
    internal SafeInputKeyComparer(SafeReadBudget budget) => _budget = budget;
    /// <summary>Charges before comparing potentially colliding external keys.</summary>
    /// <param name="x">The first key.</param>
    /// <param name="y">The second key.</param>
    /// <returns>Whether the framework values are equal.</returns>
    public bool Equals(SafeInputKey x, SafeInputKey y)
    {
        _budget.Work(1L + (x.Text?.Length ?? 0) + (y.Text?.Length ?? 0));
        return x.Kind == y.Kind && x.First == y.First && x.Second == y.Second && string.Equals(x.Text, y.Text, StringComparison.Ordinal);
    }
    /// <summary>Charges string hashing and combines the exact canonical scalar bits.</summary>
    /// <param name="obj">The framework-only key.</param>
    /// <returns>The bucket hash.</returns>
    public int GetHashCode(SafeInputKey obj)
    {
        _budget.Work(1L + (obj.Text?.Length ?? 0));
        return unchecked((int)obj.First ^ (int)(obj.First >> 32) ^ (int)obj.Second ^ (int)(obj.Second >> 32) ^ (obj.Text?.GetHashCode() ?? 0));
    }
}

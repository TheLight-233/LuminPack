namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Spells JSON name-table identifiers and computes the hashes their dispatch uses.</summary>
/// <remarks>The JSON emitter groups names by length for small tables and by XxHash3 for larger ones. Both the member suffix
/// and the hash are part of the generated lookup, so they must remain identical across generators.</remarks>
internal static class JsonNames
{
    /// <summary>Spells a member's table suffix, keeping a leading <c>U_</c> from colliding with the UTF-8 prefix.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The table suffix.</returns>
    internal static string MemberSuffix(string name) => name.TrimStart('U').StartsWith("_", System.StringComparison.Ordinal) ||
        // A field cannot share the name of its name-table class.
        name is "Utf8" or "Utf16" or "Utf8Head" or "Utf16Head" ? "U" + name : name;

    /// <summary>Computes the XxHash3 of a UTF-8 name.</summary>
    /// <param name="bytes">The encoded name.</param>
    /// <returns>The 64-bit hash.</returns>
    internal static unsafe ulong ComputeUtf8BytesHash(byte[] bytes)
    {
        fixed (byte* input = bytes)
            return ComputeXxHash3(input, bytes.Length);
    }

    /// <summary>Computes the XxHash3 of a UTF-16 name's bytes.</summary>
    /// <param name="text">The name.</param>
    /// <returns>The 64-bit hash.</returns>
    internal static unsafe ulong ComputeUtf16Hash(string text)
    {
        fixed (char* pointer = text)
        {
            byte* input = (byte*)pointer;
            return ComputeXxHash3(input, text.Length * 2);
        }
    }

    private static unsafe ulong ComputeXxHash3(byte* input, int length)
    {
        if (length == 0)
            return 12661295479421362729uL;
        if (length <= 16)
        {
            if (length > 8)
            {
                long first = *(long*)input ^ -7046029254386353067L;
                ulong last = (ulong)(*((long*)(input + length) - 1) ^ 0x27D4EB2F16566805L);
                long mixed = first + (long)last + length;
                long hashed = (mixed ^ (mixed >>> 37)) * 1609587929392839161L;
                return (ulong)(hashed ^ (hashed >>> 32));
            }
            if (length >= 4)
            {
                ulong first = *(uint*)input;
                ulong last = *((uint*)(input + length) - 1);
                ulong mixed = 2870177450012600261L + first + ((last << 32) | last);
                ulong hashed = (mixed ^ (mixed >> 37)) * 1609587929392839161L;
                return hashed ^ (hashed >> 32);
            }
            byte a = *input;
            byte b = input[length >> 1];
            byte c = input[length - 1];
            uint packed = (uint)((a << 16) | (b << 24) | c | (length << 8));
            long small = -7046029254386353131L + packed;
            long smallHashed = (small ^ (small >>> 37)) * 1609587929392839161L;
            return (ulong)(smallHashed ^ (smallHashed >>> 32));
        }
        ulong hash = (ulong)(length * -7046029288634856825L);
        int remaining = length;
        byte* pointer = input;
        while (remaining >= 16)
        {
            ulong first = *(ulong*)pointer;
            ulong second = ((ulong*)pointer)[1];
            hash += (ulong)((long)first * -7046029288634856825L);
            hash += second * 1609587929392839161L;
            pointer += 16;
            remaining -= 16;
        }
        if (remaining > 0)
        {
            if (remaining >= 8)
            {
                hash += (ulong)(*(long*)pointer * -7046029288634856825L);
                pointer += 8;
                remaining -= 8;
            }
            if (remaining >= 4)
            {
                hash += (ulong)((long)(uint)(*(int*)pointer) * 1609587929392839161L);
                pointer += 4;
                remaining -= 4;
            }
            while (remaining > 0)
            {
                hash += (ulong)(*pointer * -7046029288634856825L);
                pointer++;
                remaining--;
            }
        }
        hash ^= hash >> 37;
        hash *= 1609587929392839161L;
        return hash ^ (hash >> 32);
    }
}

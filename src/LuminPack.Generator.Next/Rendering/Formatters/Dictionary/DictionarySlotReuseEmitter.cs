using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders the existing dictionary slot refill and its staged continuation from normalized contracts.</summary>
internal static class DictionarySlotReuseEmitter
{
    /// <summary>Emits a callback-free existing-target branch with the original dense prefix and cold continuation.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "state">The operation-state variable name.</param>
    /// <param name = "target">The target used by this operation.</param>
    /// <param name = "length">The length used by this operation.</param>
    /// <param name = "rawPair">The raw pair used by this operation.</param>
    /// <param name = "returnOnMatch">Whether the slot refill completes an outlined read directly.</param>
    internal static void Emit(CodeWriter w, SharedFormatterSet set, FormatterModel model, string state, string target, string length,
        bool rawPair, bool returnOnMatch = false)
    {
        bool scalar = model.Supports("DictionaryScalarSlots");
        bool leaf = model.Supports("DictionaryLeafSlots");
        UnionModel? union = model.Supports("DictionaryUnionSlots") ? model.Arguments[1].Payload as UnionModel : null;
        UnionCaseModel[] cases = union is { WideTags: false } ? union.Cases.Where(item => item.FixedCursor && item.Model is RecordModel { Constructor: null, Rent: null, Callbacks: null }).ToArray() : Array.Empty<UnionCaseModel>();
        if (!scalar && !leaf && cases.Length == 0)
            return;
        FormatterModel key = model.Arguments[0], value = model.Arguments[1];
        string pair = "global::System.Collections.Generic.KeyValuePair<" + key.Type + ", " + value.Type + ">";
        string pool = "global::System.Buffers.ArrayPool<" + key.Type + ">.Shared";
        const string add = "global::System.Runtime.CompilerServices.Unsafe.Add";
        const string arrayReference = "global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference";
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (global::System.Object.ReferenceEquals(" + target + ".Comparer, global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default))");
        w.Open();
        w.Line("ReadSlots(ref " + state + ", " + target + ", " + length + ");");
        w.Line(returnOnMatch ? "return;" : "goto operationComplete_RootRead;");
        w.Close();
        w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        w.Line("static void ReadSlots(ref global::LuminPack.Core.LuminPackReader reader, " + model.Type + " destination, int count)");
        w.Open();
        w.Line(key.Type + "[]? seen = destination.Count == count ? null : " + pool + ".Rent(count);");
        w.Line("try");
        w.Open();
        if (scalar && rawPair)
            DensePrefix();
        else
            w.Line("int matched = 0;");
        w.Line("var order = destination.GetEnumerator();");
        w.Line("int index = matched;");
        w.Line("do");
        w.Open();
        w.Line(key.Type + " key = default;");
        FormatterElementEmitter.Read(w, set, key, "key");
        w.Line("if (seen is null && (!order.MoveNext() || !global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default.Equals(order.Current.Key, key))) seen = RecordPrefix(destination, count, index);");
        w.Line("if (seen is not null) " + add + "(ref " + arrayReference + "(seen), (nint)(uint)index) = key;");
        w.Line("ref var slot = ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrNullRef(destination, key);");
        w.Line("if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref slot)" + (scalar ? "" : " || slot is null") + ") goto remaining;");
        if (cases.Length != 0)
            UnionCases();
        else
        {
            FormatterElementEmitter.Read(w, set, value, "slot");
            w.Line("continue;");
        }

        w.Line("remaining:");
        w.Line("if (seen is null)");
        w.Open();
        w.Line("seen = RecordPrefix(destination, count, index);");
        w.Line(add + "(ref " + arrayReference + "(seen), (nint)(uint)index) = key;");
        w.Close();
        w.Line("ReadRemaining(ref reader, destination, count, seen, index);");
        w.Line("return;");
        w.Close();
        w.Line("while (++index < count);");
        w.Line("if (seen is not null)");
        w.Open();
        w.Line("var keys = new global::System.Collections.Generic.HashSet<" + key.Type + ">();");
        w.Line("ref var firstSeen = ref " + arrayReference + "(seen);");
        w.Line("index = 0;");
        w.Line("do");
        w.Open();
        w.Line("var key = " + add + "(ref firstSeen, (nint)(uint)index);");
        w.Line("if (!keys.Add(key)) destination.Add(key, destination[key]);");
        w.Close();
        w.Line("while (++index < count);");
        w.Line("var stale = new global::System.Collections.Generic.List<" + key.Type + ">();");
        w.Line("foreach (var item in destination) if (!keys.Contains(item.Key)) stale.Add(item.Key);");
        w.Line("foreach (var staleKey in stale) destination.Remove(staleKey);");
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("if (seen is not null) " + pool + ".Return(seen);");
        w.Close();
        w.Close();
        RecordPrefix();
        Remaining();
        w.Line("#endif");
        void DensePrefix()
        {
            w.Line("int matched = 0;");
            w.Line("if (seen is null && global::System.IntPtr.Size == 8)");
            w.Open();
            w.Line("var view = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(destination);");
            w.Line("if (view._freeCount == 0 && view._count == count)");
            w.Open();
            w.Line("int keyWidth = global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + key.Type + ">();");
            w.Line("int pairWidth = keyWidth + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + value.Type + ">();");
            w.Line("long payloadWidth = (long)count * pairWidth;");
            // A short payload must take the checked element path so it can publish complete values before failing.
            w.Line("if (payloadWidth <= int.MaxValue)");
            w.Open();
            w.Line("ref byte cursor = ref reader.PeekInput((int)payloadWidth, out bool completePayload);");
            w.Line("if (completePayload)");
            w.Open();
            w.Line("ref var first = ref " + arrayReference + "(view._entries!);");
            w.Line("do");
            w.Open();
            w.Line("ref var entry = ref " + add + "(ref first, (nint)(uint)matched);");
            w.Line(key.Type + " incoming = global::LuminPack.Core.LuminPackReader.Read<" + key.Type + ">(ref cursor);");
            w.Line("if (!global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default.Equals(incoming, entry.Key)) break;");
            w.Line("entry.Value = global::LuminPack.Core.LuminPackReader.Read<" + value.Type + ">(ref " + add + "(ref cursor, keyWidth));");
            w.Line("cursor = ref " + add + "(ref cursor, pairWidth);");
            w.Close();
            w.Line("while (++matched < count);");
            w.Line("reader.Advance(matched * pairWidth);");
            w.Close();
            w.Close();
            w.Close();
            w.Close();
            w.Line("if (matched == count) return;");
            w.Line("if (matched > 0) seen = RecordPrefix(destination, count, matched);");
        }

        void UnionCases()
        {
            w.Line("reader.EnsureReadable(1);");
            w.Line("var item = slot;");
            w.Line("switch (reader.Cursor)");
            w.Open();
            foreach (UnionCaseModel item in cases)
            {
                w.Line("case " + item.Tag + ":");
                w.Open();
                w.Line("if (item.GetType() != typeof(" + item.Type.GlobalName + ")) goto remaining;");
                w.Line("reader.Advance(1);");
                w.Line("var member = global::System.Runtime.CompilerServices.Unsafe.As<" + item.Type.GlobalName + ">(item);");
                w.Line("global::LuminPack.LuminPackSerializer.DeserializeUnion" + (set.Options.Interceptors ? "_" + item.Readable : "") + "<TLuminProfile>(ref reader, ref member);");
                w.Line("continue;");
                w.Close();
            }

            w.Close();
        }

        void RecordPrefix()
        {
            w.Line("static " + key.Type + "[] RecordPrefix(" + model.Type + " destination, int count, int prefix)");
            w.Open();
            w.Line("var keys = " + pool + ".Rent(count);");
            w.Line("ref var first = ref " + arrayReference + "(keys);");
            w.Line("int position = 0;");
            w.Line("foreach (var item in destination)");
            w.Open();
            w.Line("if (position == prefix) break;");
            w.Line(add + "(ref first, (nint)(uint)position++) = item.Key;");
            w.Close();
            w.Line("return keys;");
            w.Close();
        }

        void Remaining()
        {
            w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            w.Line("static void ReadRemaining(ref global::LuminPack.Core.LuminPackReader reader, " + model.Type + " destination, int count, " + key.Type + "[] seen, int parsed)");
            w.Open();
            w.Line("var items = global::System.Buffers.ArrayPool<" + pair + ">.Shared.Rent(count);");
            w.Line("try");
            w.Open();
            w.Line("ref var firstItem = ref " + arrayReference + "(items);");
            w.Line("ref var firstSeen = ref " + arrayReference + "(seen);");
            w.Line("int index = 0;");
            w.Line("if (parsed != 0)");
            w.Open();
            w.Line("do");
            w.Open();
            w.Line("var key = " + add + "(ref firstSeen, (nint)(uint)index);");
            w.Line(add + "(ref firstItem, (nint)(uint)index) = new " + pair + "(key, destination[key]);");
            w.Close();
            w.Line("while (++index < parsed);");
            w.Close();
            w.Line("do");
            w.Open();
            w.Line(key.Type + " key = default;");
            w.Line("if (index != parsed)");
            w.Open();
            FormatterElementEmitter.Read(w, set, key, "key");
            w.Close();
            w.Line("else");
            w.Indented(() => w.Line("key = " + add + "(ref firstSeen, (nint)(uint)index);"));
            w.Line(value.Type + " item = destination.TryGetValue(key, out var existing) ? existing : default!;");
            FormatterElementEmitter.Read(w, set, value, "item");
            w.Line(add + "(ref firstItem, (nint)(uint)index) = new " + pair + "(key, item);");
            w.Close();
            w.Line("while (++index < count);");
            w.Line("destination.Clear();");
            w.Line("index = 0;");
            w.Line("do");
            w.Open();
            w.Line("ref var item = ref " + add + "(ref firstItem, (nint)(uint)index);");
            w.Line("destination.Add(item.Key, item.Value);");
            w.Close();
            w.Line("while (++index < count);");
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("global::System.Buffers.ArrayPool<" + pair + ">.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + pair + ">());");
            w.Close();
            w.Close();
        }
    }
}

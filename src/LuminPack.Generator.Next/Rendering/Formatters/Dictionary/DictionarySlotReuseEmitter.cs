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
    internal static void Emit(CodeWriter w, SharedFormatterSet set, FormatterModel model, string state, string target, string length, bool rawPair)
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
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (global::System.Object.ReferenceEquals(" + target + ".Comparer, global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default))");
        w.Open();
        w.Line("ReadSlots(ref " + state + ", " + target + ", " + length + ");");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        w.Line("static void ReadSlots(ref global::LuminPack.Core.LuminPackReader reader, " + model.Type + " destination, int count)");
        w.Open();
        w.Line(key.Type + "[]? seen = destination.Count == count ? null : " + pool + ".Rent(count);");
        w.Line("try");
        w.Open();
        w.Line("var order = destination.GetEnumerator();");
        if (scalar && rawPair)
            DensePrefix();
        else
            w.Line("int matched = 0;");
        w.Line("for (int index = matched; index < count; index++)");
        w.Open();
        w.Line(key.Type + " key = default;");
        FormatterElementEmitter.Read(w, set, key, "key");
        w.Line("if (seen is null && (!order.MoveNext() || !global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default.Equals(order.Current.Key, key))) seen = RecordPrefix(destination, count, index);");
        w.Line("if (seen is not null) seen[index] = key;");
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
        w.Line("seen[index] = key;");
        w.Close();
        w.Line("ReadRemaining(ref reader, destination, count, seen, index);");
        w.Line("return;");
        w.Close();
        w.Line("if (seen is not null)");
        w.Open();
        w.Line("var keys = new global::System.Collections.Generic.HashSet<" + key.Type + ">();");
        w.Line("for (int index = 0; index < count; index++) if (!keys.Add(seen[index])) destination.Add(seen[index], destination[seen[index]]);");
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
            w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(view._entries!);");
            w.Line("for (; matched < count; matched++)");
            w.Open();
            w.Line("ref var entry = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, matched);");
            w.Line("ref byte cursor = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(reader.Buffer.Slice(reader.Position));");
            w.Line(key.Type + " incoming = global::LuminPack.Core.LuminPackReader.Read<" + key.Type + ">(ref cursor);");
            w.Line("if (!global::System.Collections.Generic.EqualityComparer<" + key.Type + ">.Default.Equals(incoming, entry.Key)) break;");
            w.Line("entry.Value = global::LuminPack.Core.LuminPackReader.Read<" + value.Type + ">(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref cursor, global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + key.Type + ">()));");
            w.Line("reader.Advance(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + key.Type + ">() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + value.Type + ">());");
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
            w.Line("int position = 0;");
            w.Line("foreach (var item in destination)");
            w.Open();
            w.Line("if (position == prefix) break;");
            w.Line("keys[position++] = item.Key;");
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
            w.Line("for (int index = 0; index < parsed; index++) items[index] = new " + pair + "(seen[index], destination[seen[index]]);");
            w.Line("for (int index = parsed; index < count; index++)");
            w.Open();
            w.Line(key.Type + " key = seen[index];");
            w.Line("if (index != parsed)");
            w.Open();
            FormatterElementEmitter.Read(w, set, key, "key");
            w.Close();
            w.Line(value.Type + " item = destination.TryGetValue(key, out var existing) ? existing : default!;");
            FormatterElementEmitter.Read(w, set, value, "item");
            w.Line("items[index] = new " + pair + "(key, item);");
            w.Close();
            w.Line("destination.Clear();");
            w.Line("for (int index = 0; index < count; index++) destination.Add(items[index].Key, items[index].Value);");
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("global::System.Buffers.ArrayPool<" + pair + ">.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + pair + ">());");
            w.Close();
            w.Close();
        }
    }
}

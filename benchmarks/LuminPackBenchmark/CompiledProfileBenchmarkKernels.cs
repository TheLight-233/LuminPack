using System.Buffers;
using LuminPack.Core;
using LuminPack.Option;
namespace LuminPackBenchmark;
/// <summary>Selects closed kernels for parameterized encoding and grammar regressions.</summary>
internal static class CompiledProfileBenchmarkKernels {
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int StringRecordLengthFor(this ref LuminPackWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.StringRecordLength<BenchmarkUtf8Length>();
case (1, false): return state.StringRecordLength<BenchmarkUtf8Token>();
case (2, false): return state.StringRecordLength<BenchmarkUtf16Length>();
case (3, false): return state.StringRecordLength<BenchmarkUtf16Token>();
case (0, true): return state.StringRecordLength<BenchmarkStrictUtf8Length>();
case (1, true): return state.StringRecordLength<BenchmarkStrictUtf8Token>();
case (2, true): return state.StringRecordLength<BenchmarkStrictUtf16Length>();
case (3, true): return state.StringRecordLength<BenchmarkStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
internal static void WriteNullStringHeaderFor(this ref LuminPackWriter state, BenchmarkProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNullStringHeader<BenchmarkUtf8Length>(ref index); return;
case (1, false): state.WriteNullStringHeader<BenchmarkUtf8Token>(ref index); return;
case (2, false): state.WriteNullStringHeader<BenchmarkUtf16Length>(ref index); return;
case (3, false): state.WriteNullStringHeader<BenchmarkUtf16Token>(ref index); return;
case (0, true): state.WriteNullStringHeader<BenchmarkStrictUtf8Length>(ref index); return;
case (1, true): state.WriteNullStringHeader<BenchmarkStrictUtf8Token>(ref index); return;
case (2, true): state.WriteNullStringHeader<BenchmarkStrictUtf16Length>(ref index); return;
case (3, true): state.WriteNullStringHeader<BenchmarkStrictUtf16Token>(ref index); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="offset">The original kernel argument.</param>
internal static void WriteNullStringHeaderFor(this ref LuminPackWriter state, BenchmarkProfile profile, ref int index, out int offset) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNullStringHeader<BenchmarkUtf8Length>(ref index, out offset); return;
case (1, false): state.WriteNullStringHeader<BenchmarkUtf8Token>(ref index, out offset); return;
case (2, false): state.WriteNullStringHeader<BenchmarkUtf16Length>(ref index, out offset); return;
case (3, false): state.WriteNullStringHeader<BenchmarkUtf16Token>(ref index, out offset); return;
case (0, true): state.WriteNullStringHeader<BenchmarkStrictUtf8Length>(ref index, out offset); return;
case (1, true): state.WriteNullStringHeader<BenchmarkStrictUtf8Token>(ref index, out offset); return;
case (2, true): state.WriteNullStringHeader<BenchmarkStrictUtf16Length>(ref index, out offset); return;
case (3, true): state.WriteNullStringHeader<BenchmarkStrictUtf16Token>(ref index, out offset); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, BenchmarkProfile profile, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<BenchmarkUtf8Length>(value);
case (1, false): return state.WriteString<BenchmarkUtf8Token>(value);
case (2, false): return state.WriteString<BenchmarkUtf16Length>(value);
case (3, false): return state.WriteString<BenchmarkUtf16Token>(value);
case (0, true): return state.WriteString<BenchmarkStrictUtf8Length>(value);
case (1, true): return state.WriteString<BenchmarkStrictUtf8Token>(value);
case (2, true): return state.WriteString<BenchmarkStrictUtf16Length>(value);
case (3, true): return state.WriteString<BenchmarkStrictUtf16Token>(value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackWriter state, BenchmarkProfile profile, string? value, int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<BenchmarkUtf8Length>(value, length); return;
case (1, false): state.WriteString<BenchmarkUtf8Token>(value, length); return;
case (2, false): state.WriteString<BenchmarkUtf16Length>(value, length); return;
case (3, false): state.WriteString<BenchmarkUtf16Token>(value, length); return;
case (0, true): state.WriteString<BenchmarkStrictUtf8Length>(value, length); return;
case (1, true): state.WriteString<BenchmarkStrictUtf8Token>(value, length); return;
case (2, true): state.WriteString<BenchmarkStrictUtf16Length>(value, length); return;
case (3, true): state.WriteString<BenchmarkStrictUtf16Token>(value, length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, BenchmarkProfile profile, ref int index, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<BenchmarkUtf8Length>(ref index, value);
case (1, false): return state.WriteString<BenchmarkUtf8Token>(ref index, value);
case (2, false): return state.WriteString<BenchmarkUtf16Length>(ref index, value);
case (3, false): return state.WriteString<BenchmarkUtf16Token>(ref index, value);
case (0, true): return state.WriteString<BenchmarkStrictUtf8Length>(ref index, value);
case (1, true): return state.WriteString<BenchmarkStrictUtf8Token>(ref index, value);
case (2, true): return state.WriteString<BenchmarkStrictUtf16Length>(ref index, value);
case (3, true): return state.WriteString<BenchmarkStrictUtf16Token>(ref index, value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackWriter state, BenchmarkProfile profile, ref int index, string? value, int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<BenchmarkUtf8Length>(ref index, value, length); return;
case (1, false): state.WriteString<BenchmarkUtf8Token>(ref index, value, length); return;
case (2, false): state.WriteString<BenchmarkUtf16Length>(ref index, value, length); return;
case (3, false): state.WriteString<BenchmarkUtf16Token>(ref index, value, length); return;
case (0, true): state.WriteString<BenchmarkStrictUtf8Length>(ref index, value, length); return;
case (1, true): state.WriteString<BenchmarkStrictUtf8Token>(ref index, value, length); return;
case (2, true): state.WriteString<BenchmarkStrictUtf16Length>(ref index, value, length); return;
case (3, true): state.WriteString<BenchmarkStrictUtf16Token>(ref index, value, length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, BenchmarkProfile profile, ref int index, ReadOnlySpan<char> value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<BenchmarkUtf8Length>(ref index, value);
case (1, false): return state.WriteString<BenchmarkUtf8Token>(ref index, value);
case (2, false): return state.WriteString<BenchmarkUtf16Length>(ref index, value);
case (3, false): return state.WriteString<BenchmarkUtf16Token>(ref index, value);
case (0, true): return state.WriteString<BenchmarkStrictUtf8Length>(ref index, value);
case (1, true): return state.WriteString<BenchmarkStrictUtf8Token>(ref index, value);
case (2, true): return state.WriteString<BenchmarkStrictUtf16Length>(ref index, value);
case (3, true): return state.WriteString<BenchmarkStrictUtf16Token>(ref index, value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static bool PeekIsNullStringFor(this ref LuminPackReader state, BenchmarkProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.PeekIsNullString<BenchmarkUtf8Length>(ref index);
case (1, false): return state.PeekIsNullString<BenchmarkUtf8Token>(ref index);
case (2, false): return state.PeekIsNullString<BenchmarkUtf16Length>(ref index);
case (3, false): return state.PeekIsNullString<BenchmarkUtf16Token>(ref index);
case (0, true): return state.PeekIsNullString<BenchmarkStrictUtf8Length>(ref index);
case (1, true): return state.PeekIsNullString<BenchmarkStrictUtf8Token>(ref index);
case (2, true): return state.PeekIsNullString<BenchmarkStrictUtf16Length>(ref index);
case (3, true): return state.PeekIsNullString<BenchmarkStrictUtf16Token>(ref index);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void ReadStringLengthFor(this ref LuminPackReader state, BenchmarkProfile profile, ref int index, out int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.ReadStringLength<BenchmarkUtf8Length>(ref index, out length); return;
case (1, false): state.ReadStringLength<BenchmarkUtf8Token>(ref index, out length); return;
case (2, false): state.ReadStringLength<BenchmarkUtf16Length>(ref index, out length); return;
case (3, false): state.ReadStringLength<BenchmarkUtf16Token>(ref index, out length); return;
case (0, true): state.ReadStringLength<BenchmarkStrictUtf8Length>(ref index, out length); return;
case (1, true): state.ReadStringLength<BenchmarkStrictUtf8Token>(ref index, out length); return;
case (2, true): state.ReadStringLength<BenchmarkStrictUtf16Length>(ref index, out length); return;
case (3, true): state.ReadStringLength<BenchmarkStrictUtf16Token>(ref index, out length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int StringRecordLengthFor(this ref LuminPackReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.StringRecordLength<BenchmarkUtf8Length>();
case (1, false): return state.StringRecordLength<BenchmarkUtf8Token>();
case (2, false): return state.StringRecordLength<BenchmarkUtf16Length>();
case (3, false): return state.StringRecordLength<BenchmarkUtf16Token>();
case (0, true): return state.StringRecordLength<BenchmarkStrictUtf8Length>();
case (1, true): return state.StringRecordLength<BenchmarkStrictUtf8Token>();
case (2, true): return state.StringRecordLength<BenchmarkStrictUtf16Length>();
case (3, true): return state.StringRecordLength<BenchmarkStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static string? ReadStringAndAdvanceFor(this ref LuminPackReader state, BenchmarkProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadStringAndAdvance<BenchmarkUtf8Length>(ref index);
case (1, false): return state.ReadStringAndAdvance<BenchmarkUtf8Token>(ref index);
case (2, false): return state.ReadStringAndAdvance<BenchmarkUtf16Length>(ref index);
case (3, false): return state.ReadStringAndAdvance<BenchmarkUtf16Token>(ref index);
case (0, true): return state.ReadStringAndAdvance<BenchmarkStrictUtf8Length>(ref index);
case (1, true): return state.ReadStringAndAdvance<BenchmarkStrictUtf8Token>(ref index);
case (2, true): return state.ReadStringAndAdvance<BenchmarkStrictUtf16Length>(ref index);
case (3, true): return state.ReadStringAndAdvance<BenchmarkStrictUtf16Token>(ref index);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteByteRawFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, byte value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteByteRaw<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteByteRaw<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteByteRaw<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteByteRaw<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteByteRaw<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteByteRaw<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteByteRaw<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteByteRaw<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ulong first, ulong last, int byteCount, int value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<BenchmarkUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<BenchmarkUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<BenchmarkUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<BenchmarkUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ulong first, ulong last, int byteCount, uint value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<BenchmarkUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<BenchmarkUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<BenchmarkUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<BenchmarkUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ulong first, ulong last, int byteCount, long value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<BenchmarkUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<BenchmarkUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<BenchmarkUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<BenchmarkUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ulong first, ulong last, int byteCount, ulong value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<BenchmarkUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<BenchmarkUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<BenchmarkUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<BenchmarkUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<BenchmarkStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<BenchmarkStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteObjectStartFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteObjectStart<BenchmarkUtf8Length>(); return;
case (1, false): state.WriteObjectStart<BenchmarkUtf8Token>(); return;
case (2, false): state.WriteObjectStart<BenchmarkUtf16Length>(); return;
case (3, false): state.WriteObjectStart<BenchmarkUtf16Token>(); return;
case (0, true): state.WriteObjectStart<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.WriteObjectStart<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.WriteObjectStart<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.WriteObjectStart<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteObjectEndFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteObjectEnd<BenchmarkUtf8Length>(); return;
case (1, false): state.WriteObjectEnd<BenchmarkUtf8Token>(); return;
case (2, false): state.WriteObjectEnd<BenchmarkUtf16Length>(); return;
case (3, false): state.WriteObjectEnd<BenchmarkUtf16Token>(); return;
case (0, true): state.WriteObjectEnd<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.WriteObjectEnd<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.WriteObjectEnd<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.WriteObjectEnd<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteArrayStartFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteArrayStart<BenchmarkUtf8Length>(); return;
case (1, false): state.WriteArrayStart<BenchmarkUtf8Token>(); return;
case (2, false): state.WriteArrayStart<BenchmarkUtf16Length>(); return;
case (3, false): state.WriteArrayStart<BenchmarkUtf16Token>(); return;
case (0, true): state.WriteArrayStart<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.WriteArrayStart<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.WriteArrayStart<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.WriteArrayStart<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteArrayEndFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteArrayEnd<BenchmarkUtf8Length>(); return;
case (1, false): state.WriteArrayEnd<BenchmarkUtf8Token>(); return;
case (2, false): state.WriteArrayEnd<BenchmarkUtf16Length>(); return;
case (3, false): state.WriteArrayEnd<BenchmarkUtf16Token>(); return;
case (0, true): state.WriteArrayEnd<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.WriteArrayEnd<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.WriteArrayEnd<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.WriteArrayEnd<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="utf8PropertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ReadOnlySpan<byte> utf8PropertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<BenchmarkUtf8Length>(utf8PropertyName); return;
case (1, false): state.WritePropertyName<BenchmarkUtf8Token>(utf8PropertyName); return;
case (2, false): state.WritePropertyName<BenchmarkUtf16Length>(utf8PropertyName); return;
case (3, false): state.WritePropertyName<BenchmarkUtf16Token>(utf8PropertyName); return;
case (0, true): state.WritePropertyName<BenchmarkStrictUtf8Length>(utf8PropertyName); return;
case (1, true): state.WritePropertyName<BenchmarkStrictUtf8Token>(utf8PropertyName); return;
case (2, true): state.WritePropertyName<BenchmarkStrictUtf16Length>(utf8PropertyName); return;
case (3, true): state.WritePropertyName<BenchmarkStrictUtf16Token>(utf8PropertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="utf16PropertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, ReadOnlySpan<char> utf16PropertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<BenchmarkUtf8Length>(utf16PropertyName); return;
case (1, false): state.WritePropertyName<BenchmarkUtf8Token>(utf16PropertyName); return;
case (2, false): state.WritePropertyName<BenchmarkUtf16Length>(utf16PropertyName); return;
case (3, false): state.WritePropertyName<BenchmarkUtf16Token>(utf16PropertyName); return;
case (0, true): state.WritePropertyName<BenchmarkStrictUtf8Length>(utf16PropertyName); return;
case (1, true): state.WritePropertyName<BenchmarkStrictUtf8Token>(utf16PropertyName); return;
case (2, true): state.WritePropertyName<BenchmarkStrictUtf16Length>(utf16PropertyName); return;
case (3, true): state.WritePropertyName<BenchmarkStrictUtf16Token>(utf16PropertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="propertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, string propertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<BenchmarkUtf8Length>(propertyName); return;
case (1, false): state.WritePropertyName<BenchmarkUtf8Token>(propertyName); return;
case (2, false): state.WritePropertyName<BenchmarkUtf16Length>(propertyName); return;
case (3, false): state.WritePropertyName<BenchmarkUtf16Token>(propertyName); return;
case (0, true): state.WritePropertyName<BenchmarkStrictUtf8Length>(propertyName); return;
case (1, true): state.WritePropertyName<BenchmarkStrictUtf8Token>(propertyName); return;
case (2, true): state.WritePropertyName<BenchmarkStrictUtf16Length>(propertyName); return;
case (3, true): state.WritePropertyName<BenchmarkStrictUtf16Token>(propertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteString<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteString<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteString<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteString<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteString<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteString<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteString<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteFloatFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, float value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteFloat<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteFloat<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteFloat<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteFloat<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteFloat<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteFloat<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteFloat<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteFloat<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteDoubleFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, double value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteDouble<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteDouble<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteDouble<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteDouble<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteDouble<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteDouble<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteDouble<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteDouble<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteIntFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, int value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteInt<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteInt<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteInt<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteInt<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteInt<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteInt<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteInt<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteInt<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteLongFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, long value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteLong<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteLong<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteLong<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteLong<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteLong<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteLong<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteLong<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteLong<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteBoolFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile, bool value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteBool<BenchmarkUtf8Length>(value); return;
case (1, false): state.WriteBool<BenchmarkUtf8Token>(value); return;
case (2, false): state.WriteBool<BenchmarkUtf16Length>(value); return;
case (3, false): state.WriteBool<BenchmarkUtf16Token>(value); return;
case (0, true): state.WriteBool<BenchmarkStrictUtf8Length>(value); return;
case (1, true): state.WriteBool<BenchmarkStrictUtf8Token>(value); return;
case (2, true): state.WriteBool<BenchmarkStrictUtf16Length>(value); return;
case (3, true): state.WriteBool<BenchmarkStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteNullFor(this ref LuminPackJsonWriter state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNull<BenchmarkUtf8Length>(); return;
case (1, false): state.WriteNull<BenchmarkUtf8Token>(); return;
case (2, false): state.WriteNull<BenchmarkUtf16Length>(); return;
case (3, false): state.WriteNull<BenchmarkUtf16Token>(); return;
case (0, true): state.WriteNull<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.WriteNull<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.WriteNull<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.WriteNull<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static bool ReadFor(this ref LuminPackJsonReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.Read<BenchmarkUtf8Length>();
case (1, false): return state.Read<BenchmarkUtf8Token>();
case (2, false): return state.Read<BenchmarkUtf16Length>();
case (3, false): return state.Read<BenchmarkUtf16Token>();
case (0, true): return state.Read<BenchmarkStrictUtf8Length>();
case (1, true): return state.Read<BenchmarkStrictUtf8Token>();
case (2, true): return state.Read<BenchmarkStrictUtf16Length>();
case (3, true): return state.Read<BenchmarkStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void SkipFor(this ref LuminPackJsonReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Skip<BenchmarkUtf8Length>(); return;
case (1, false): state.Skip<BenchmarkUtf8Token>(); return;
case (2, false): state.Skip<BenchmarkUtf16Length>(); return;
case (3, false): state.Skip<BenchmarkUtf16Token>(); return;
case (0, true): state.Skip<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.Skip<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.Skip<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.Skip<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void EnsureEndOfDocumentFor(this ref LuminPackJsonReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.EnsureEndOfDocument<BenchmarkUtf8Length>(); return;
case (1, false): state.EnsureEndOfDocument<BenchmarkUtf8Token>(); return;
case (2, false): state.EnsureEndOfDocument<BenchmarkUtf16Length>(); return;
case (3, false): state.EnsureEndOfDocument<BenchmarkUtf16Token>(); return;
case (0, true): state.EnsureEndOfDocument<BenchmarkStrictUtf8Length>(); return;
case (1, true): state.EnsureEndOfDocument<BenchmarkStrictUtf8Token>(); return;
case (2, true): state.EnsureEndOfDocument<BenchmarkStrictUtf16Length>(); return;
case (3, true): state.EnsureEndOfDocument<BenchmarkStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static float ReadNextFloatValueFor(this ref LuminPackJsonReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadNextFloatValue<BenchmarkUtf8Length>();
case (1, false): return state.ReadNextFloatValue<BenchmarkUtf8Token>();
case (2, false): return state.ReadNextFloatValue<BenchmarkUtf16Length>();
case (3, false): return state.ReadNextFloatValue<BenchmarkUtf16Token>();
case (0, true): return state.ReadNextFloatValue<BenchmarkStrictUtf8Length>();
case (1, true): return state.ReadNextFloatValue<BenchmarkStrictUtf8Token>();
case (2, true): return state.ReadNextFloatValue<BenchmarkStrictUtf16Length>();
case (3, true): return state.ReadNextFloatValue<BenchmarkStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static double ReadDoubleFor(this ref LuminPackJsonReader state, BenchmarkProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadDouble<BenchmarkUtf8Length>();
case (1, false): return state.ReadDouble<BenchmarkUtf8Token>();
case (2, false): return state.ReadDouble<BenchmarkUtf16Length>();
case (3, false): return state.ReadDouble<BenchmarkUtf16Token>();
case (0, true): return state.ReadDouble<BenchmarkStrictUtf8Length>();
case (1, true): return state.ReadDouble<BenchmarkStrictUtf8Token>();
case (2, true): return state.ReadDouble<BenchmarkStrictUtf16Length>();
case (3, true): return state.ReadDouble<BenchmarkStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
}

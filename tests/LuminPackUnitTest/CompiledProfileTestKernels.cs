using System.Buffers;
using LuminPack.Core;
using LuminPack.Option;
namespace LuminPackUnitTest;
/// <summary>Selects closed kernels for parameterized encoding and grammar regressions.</summary>
internal static class CompiledProfileTestKernels {
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int StringRecordLengthFor(this ref LuminPackWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.StringRecordLength<TestUtf8Length>();
case (1, false): return state.StringRecordLength<TestUtf8Token>();
case (2, false): return state.StringRecordLength<TestUtf16Length>();
case (3, false): return state.StringRecordLength<TestUtf16Token>();
case (0, true): return state.StringRecordLength<TestStrictUtf8Length>();
case (1, true): return state.StringRecordLength<TestStrictUtf8Token>();
case (2, true): return state.StringRecordLength<TestStrictUtf16Length>();
case (3, true): return state.StringRecordLength<TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
internal static void WriteNullStringHeaderFor(this ref LuminPackWriter state, TestSerializationProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNullStringHeader<TestUtf8Length>(ref index); return;
case (1, false): state.WriteNullStringHeader<TestUtf8Token>(ref index); return;
case (2, false): state.WriteNullStringHeader<TestUtf16Length>(ref index); return;
case (3, false): state.WriteNullStringHeader<TestUtf16Token>(ref index); return;
case (0, true): state.WriteNullStringHeader<TestStrictUtf8Length>(ref index); return;
case (1, true): state.WriteNullStringHeader<TestStrictUtf8Token>(ref index); return;
case (2, true): state.WriteNullStringHeader<TestStrictUtf16Length>(ref index); return;
case (3, true): state.WriteNullStringHeader<TestStrictUtf16Token>(ref index); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="offset">The original kernel argument.</param>
internal static void WriteNullStringHeaderFor(this ref LuminPackWriter state, TestSerializationProfile profile, ref int index, out int offset) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNullStringHeader<TestUtf8Length>(ref index, out offset); return;
case (1, false): state.WriteNullStringHeader<TestUtf8Token>(ref index, out offset); return;
case (2, false): state.WriteNullStringHeader<TestUtf16Length>(ref index, out offset); return;
case (3, false): state.WriteNullStringHeader<TestUtf16Token>(ref index, out offset); return;
case (0, true): state.WriteNullStringHeader<TestStrictUtf8Length>(ref index, out offset); return;
case (1, true): state.WriteNullStringHeader<TestStrictUtf8Token>(ref index, out offset); return;
case (2, true): state.WriteNullStringHeader<TestStrictUtf16Length>(ref index, out offset); return;
case (3, true): state.WriteNullStringHeader<TestStrictUtf16Token>(ref index, out offset); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, TestSerializationProfile profile, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<TestUtf8Length>(value);
case (1, false): return state.WriteString<TestUtf8Token>(value);
case (2, false): return state.WriteString<TestUtf16Length>(value);
case (3, false): return state.WriteString<TestUtf16Token>(value);
case (0, true): return state.WriteString<TestStrictUtf8Length>(value);
case (1, true): return state.WriteString<TestStrictUtf8Token>(value);
case (2, true): return state.WriteString<TestStrictUtf16Length>(value);
case (3, true): return state.WriteString<TestStrictUtf16Token>(value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackWriter state, TestSerializationProfile profile, string? value, int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<TestUtf8Length>(value, length); return;
case (1, false): state.WriteString<TestUtf8Token>(value, length); return;
case (2, false): state.WriteString<TestUtf16Length>(value, length); return;
case (3, false): state.WriteString<TestUtf16Token>(value, length); return;
case (0, true): state.WriteString<TestStrictUtf8Length>(value, length); return;
case (1, true): state.WriteString<TestStrictUtf8Token>(value, length); return;
case (2, true): state.WriteString<TestStrictUtf16Length>(value, length); return;
case (3, true): state.WriteString<TestStrictUtf16Token>(value, length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, TestSerializationProfile profile, ref int index, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<TestUtf8Length>(ref index, value);
case (1, false): return state.WriteString<TestUtf8Token>(ref index, value);
case (2, false): return state.WriteString<TestUtf16Length>(ref index, value);
case (3, false): return state.WriteString<TestUtf16Token>(ref index, value);
case (0, true): return state.WriteString<TestStrictUtf8Length>(ref index, value);
case (1, true): return state.WriteString<TestStrictUtf8Token>(ref index, value);
case (2, true): return state.WriteString<TestStrictUtf16Length>(ref index, value);
case (3, true): return state.WriteString<TestStrictUtf16Token>(ref index, value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackWriter state, TestSerializationProfile profile, ref int index, string? value, int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<TestUtf8Length>(ref index, value, length); return;
case (1, false): state.WriteString<TestUtf8Token>(ref index, value, length); return;
case (2, false): state.WriteString<TestUtf16Length>(ref index, value, length); return;
case (3, false): state.WriteString<TestUtf16Token>(ref index, value, length); return;
case (0, true): state.WriteString<TestStrictUtf8Length>(ref index, value, length); return;
case (1, true): state.WriteString<TestStrictUtf8Token>(ref index, value, length); return;
case (2, true): state.WriteString<TestStrictUtf16Length>(ref index, value, length); return;
case (3, true): state.WriteString<TestStrictUtf16Token>(ref index, value, length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int WriteStringFor(this ref LuminPackWriter state, TestSerializationProfile profile, ref int index, ReadOnlySpan<char> value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.WriteString<TestUtf8Length>(ref index, value);
case (1, false): return state.WriteString<TestUtf8Token>(ref index, value);
case (2, false): return state.WriteString<TestUtf16Length>(ref index, value);
case (3, false): return state.WriteString<TestUtf16Token>(ref index, value);
case (0, true): return state.WriteString<TestStrictUtf8Length>(ref index, value);
case (1, true): return state.WriteString<TestStrictUtf8Token>(ref index, value);
case (2, true): return state.WriteString<TestStrictUtf16Length>(ref index, value);
case (3, true): return state.WriteString<TestStrictUtf16Token>(ref index, value);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static bool PeekIsNullStringFor(this ref LuminPackReader state, TestSerializationProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.PeekIsNullString<TestUtf8Length>(ref index);
case (1, false): return state.PeekIsNullString<TestUtf8Token>(ref index);
case (2, false): return state.PeekIsNullString<TestUtf16Length>(ref index);
case (3, false): return state.PeekIsNullString<TestUtf16Token>(ref index);
case (0, true): return state.PeekIsNullString<TestStrictUtf8Length>(ref index);
case (1, true): return state.PeekIsNullString<TestStrictUtf8Token>(ref index);
case (2, true): return state.PeekIsNullString<TestStrictUtf16Length>(ref index);
case (3, true): return state.PeekIsNullString<TestStrictUtf16Token>(ref index);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <param name="length">The original kernel argument.</param>
internal static void ReadStringLengthFor(this ref LuminPackReader state, TestSerializationProfile profile, ref int index, out int length) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.ReadStringLength<TestUtf8Length>(ref index, out length); return;
case (1, false): state.ReadStringLength<TestUtf8Token>(ref index, out length); return;
case (2, false): state.ReadStringLength<TestUtf16Length>(ref index, out length); return;
case (3, false): state.ReadStringLength<TestUtf16Token>(ref index, out length); return;
case (0, true): state.ReadStringLength<TestStrictUtf8Length>(ref index, out length); return;
case (1, true): state.ReadStringLength<TestStrictUtf8Token>(ref index, out length); return;
case (2, true): state.ReadStringLength<TestStrictUtf16Length>(ref index, out length); return;
case (3, true): state.ReadStringLength<TestStrictUtf16Token>(ref index, out length); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static int StringRecordLengthFor(this ref LuminPackReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.StringRecordLength<TestUtf8Length>();
case (1, false): return state.StringRecordLength<TestUtf8Token>();
case (2, false): return state.StringRecordLength<TestUtf16Length>();
case (3, false): return state.StringRecordLength<TestUtf16Token>();
case (0, true): return state.StringRecordLength<TestStrictUtf8Length>();
case (1, true): return state.StringRecordLength<TestStrictUtf8Token>();
case (2, true): return state.StringRecordLength<TestStrictUtf16Length>();
case (3, true): return state.StringRecordLength<TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="index">The original kernel argument.</param>
/// <returns>The unchanged kernel result.</returns>
internal static string? ReadStringAndAdvanceFor(this ref LuminPackReader state, TestSerializationProfile profile, ref int index) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadStringAndAdvance<TestUtf8Length>(ref index);
case (1, false): return state.ReadStringAndAdvance<TestUtf8Token>(ref index);
case (2, false): return state.ReadStringAndAdvance<TestUtf16Length>(ref index);
case (3, false): return state.ReadStringAndAdvance<TestUtf16Token>(ref index);
case (0, true): return state.ReadStringAndAdvance<TestStrictUtf8Length>(ref index);
case (1, true): return state.ReadStringAndAdvance<TestStrictUtf8Token>(ref index);
case (2, true): return state.ReadStringAndAdvance<TestStrictUtf16Length>(ref index);
case (3, true): return state.ReadStringAndAdvance<TestStrictUtf16Token>(ref index);
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteByteRawFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, byte value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteByteRaw<TestUtf8Length>(value); return;
case (1, false): state.WriteByteRaw<TestUtf8Token>(value); return;
case (2, false): state.WriteByteRaw<TestUtf16Length>(value); return;
case (3, false): state.WriteByteRaw<TestUtf16Token>(value); return;
case (0, true): state.WriteByteRaw<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteByteRaw<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteByteRaw<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteByteRaw<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ulong first, ulong last, int byteCount, int value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<TestUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<TestUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<TestUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<TestUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<TestStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<TestStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<TestStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<TestStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ulong first, ulong last, int byteCount, uint value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<TestUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<TestUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<TestUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<TestUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<TestStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<TestStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<TestStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<TestStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ulong first, ulong last, int byteCount, long value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<TestUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<TestUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<TestUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<TestUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<TestStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<TestStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<TestStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<TestStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="first">The original kernel argument.</param>
/// <param name="last">The original kernel argument.</param>
/// <param name="byteCount">The original kernel argument.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteGeneratedIntegerFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ulong first, ulong last, int byteCount, ulong value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteGeneratedInteger<TestUtf8Length>(first, last, byteCount, value); return;
case (1, false): state.WriteGeneratedInteger<TestUtf8Token>(first, last, byteCount, value); return;
case (2, false): state.WriteGeneratedInteger<TestUtf16Length>(first, last, byteCount, value); return;
case (3, false): state.WriteGeneratedInteger<TestUtf16Token>(first, last, byteCount, value); return;
case (0, true): state.WriteGeneratedInteger<TestStrictUtf8Length>(first, last, byteCount, value); return;
case (1, true): state.WriteGeneratedInteger<TestStrictUtf8Token>(first, last, byteCount, value); return;
case (2, true): state.WriteGeneratedInteger<TestStrictUtf16Length>(first, last, byteCount, value); return;
case (3, true): state.WriteGeneratedInteger<TestStrictUtf16Token>(first, last, byteCount, value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteObjectStartFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteObjectStart<TestUtf8Length>(); return;
case (1, false): state.WriteObjectStart<TestUtf8Token>(); return;
case (2, false): state.WriteObjectStart<TestUtf16Length>(); return;
case (3, false): state.WriteObjectStart<TestUtf16Token>(); return;
case (0, true): state.WriteObjectStart<TestStrictUtf8Length>(); return;
case (1, true): state.WriteObjectStart<TestStrictUtf8Token>(); return;
case (2, true): state.WriteObjectStart<TestStrictUtf16Length>(); return;
case (3, true): state.WriteObjectStart<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteObjectEndFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteObjectEnd<TestUtf8Length>(); return;
case (1, false): state.WriteObjectEnd<TestUtf8Token>(); return;
case (2, false): state.WriteObjectEnd<TestUtf16Length>(); return;
case (3, false): state.WriteObjectEnd<TestUtf16Token>(); return;
case (0, true): state.WriteObjectEnd<TestStrictUtf8Length>(); return;
case (1, true): state.WriteObjectEnd<TestStrictUtf8Token>(); return;
case (2, true): state.WriteObjectEnd<TestStrictUtf16Length>(); return;
case (3, true): state.WriteObjectEnd<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteArrayStartFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteArrayStart<TestUtf8Length>(); return;
case (1, false): state.WriteArrayStart<TestUtf8Token>(); return;
case (2, false): state.WriteArrayStart<TestUtf16Length>(); return;
case (3, false): state.WriteArrayStart<TestUtf16Token>(); return;
case (0, true): state.WriteArrayStart<TestStrictUtf8Length>(); return;
case (1, true): state.WriteArrayStart<TestStrictUtf8Token>(); return;
case (2, true): state.WriteArrayStart<TestStrictUtf16Length>(); return;
case (3, true): state.WriteArrayStart<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteArrayEndFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteArrayEnd<TestUtf8Length>(); return;
case (1, false): state.WriteArrayEnd<TestUtf8Token>(); return;
case (2, false): state.WriteArrayEnd<TestUtf16Length>(); return;
case (3, false): state.WriteArrayEnd<TestUtf16Token>(); return;
case (0, true): state.WriteArrayEnd<TestStrictUtf8Length>(); return;
case (1, true): state.WriteArrayEnd<TestStrictUtf8Token>(); return;
case (2, true): state.WriteArrayEnd<TestStrictUtf16Length>(); return;
case (3, true): state.WriteArrayEnd<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="utf8PropertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ReadOnlySpan<byte> utf8PropertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<TestUtf8Length>(utf8PropertyName); return;
case (1, false): state.WritePropertyName<TestUtf8Token>(utf8PropertyName); return;
case (2, false): state.WritePropertyName<TestUtf16Length>(utf8PropertyName); return;
case (3, false): state.WritePropertyName<TestUtf16Token>(utf8PropertyName); return;
case (0, true): state.WritePropertyName<TestStrictUtf8Length>(utf8PropertyName); return;
case (1, true): state.WritePropertyName<TestStrictUtf8Token>(utf8PropertyName); return;
case (2, true): state.WritePropertyName<TestStrictUtf16Length>(utf8PropertyName); return;
case (3, true): state.WritePropertyName<TestStrictUtf16Token>(utf8PropertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="utf16PropertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, ReadOnlySpan<char> utf16PropertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<TestUtf8Length>(utf16PropertyName); return;
case (1, false): state.WritePropertyName<TestUtf8Token>(utf16PropertyName); return;
case (2, false): state.WritePropertyName<TestUtf16Length>(utf16PropertyName); return;
case (3, false): state.WritePropertyName<TestUtf16Token>(utf16PropertyName); return;
case (0, true): state.WritePropertyName<TestStrictUtf8Length>(utf16PropertyName); return;
case (1, true): state.WritePropertyName<TestStrictUtf8Token>(utf16PropertyName); return;
case (2, true): state.WritePropertyName<TestStrictUtf16Length>(utf16PropertyName); return;
case (3, true): state.WritePropertyName<TestStrictUtf16Token>(utf16PropertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="propertyName">The original kernel argument.</param>
internal static void WritePropertyNameFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, string propertyName) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WritePropertyName<TestUtf8Length>(propertyName); return;
case (1, false): state.WritePropertyName<TestUtf8Token>(propertyName); return;
case (2, false): state.WritePropertyName<TestUtf16Length>(propertyName); return;
case (3, false): state.WritePropertyName<TestUtf16Token>(propertyName); return;
case (0, true): state.WritePropertyName<TestStrictUtf8Length>(propertyName); return;
case (1, true): state.WritePropertyName<TestStrictUtf8Token>(propertyName); return;
case (2, true): state.WritePropertyName<TestStrictUtf16Length>(propertyName); return;
case (3, true): state.WritePropertyName<TestStrictUtf16Token>(propertyName); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteStringFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteString<TestUtf8Length>(value); return;
case (1, false): state.WriteString<TestUtf8Token>(value); return;
case (2, false): state.WriteString<TestUtf16Length>(value); return;
case (3, false): state.WriteString<TestUtf16Token>(value); return;
case (0, true): state.WriteString<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteString<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteString<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteString<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteFloatFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, float value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteFloat<TestUtf8Length>(value); return;
case (1, false): state.WriteFloat<TestUtf8Token>(value); return;
case (2, false): state.WriteFloat<TestUtf16Length>(value); return;
case (3, false): state.WriteFloat<TestUtf16Token>(value); return;
case (0, true): state.WriteFloat<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteFloat<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteFloat<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteFloat<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteDoubleFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, double value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteDouble<TestUtf8Length>(value); return;
case (1, false): state.WriteDouble<TestUtf8Token>(value); return;
case (2, false): state.WriteDouble<TestUtf16Length>(value); return;
case (3, false): state.WriteDouble<TestUtf16Token>(value); return;
case (0, true): state.WriteDouble<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteDouble<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteDouble<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteDouble<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteIntFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, int value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteInt<TestUtf8Length>(value); return;
case (1, false): state.WriteInt<TestUtf8Token>(value); return;
case (2, false): state.WriteInt<TestUtf16Length>(value); return;
case (3, false): state.WriteInt<TestUtf16Token>(value); return;
case (0, true): state.WriteInt<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteInt<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteInt<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteInt<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteLongFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, long value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteLong<TestUtf8Length>(value); return;
case (1, false): state.WriteLong<TestUtf8Token>(value); return;
case (2, false): state.WriteLong<TestUtf16Length>(value); return;
case (3, false): state.WriteLong<TestUtf16Token>(value); return;
case (0, true): state.WriteLong<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteLong<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteLong<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteLong<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteBoolFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, bool value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteBool<TestUtf8Length>(value); return;
case (1, false): state.WriteBool<TestUtf8Token>(value); return;
case (2, false): state.WriteBool<TestUtf16Length>(value); return;
case (3, false): state.WriteBool<TestUtf16Token>(value); return;
case (0, true): state.WriteBool<TestStrictUtf8Length>(value); return;
case (1, true): state.WriteBool<TestStrictUtf8Token>(value); return;
case (2, true): state.WriteBool<TestStrictUtf16Length>(value); return;
case (3, true): state.WriteBool<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void WriteNullFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.WriteNull<TestUtf8Length>(); return;
case (1, false): state.WriteNull<TestUtf8Token>(); return;
case (2, false): state.WriteNull<TestUtf16Length>(); return;
case (3, false): state.WriteNull<TestUtf16Token>(); return;
case (0, true): state.WriteNull<TestStrictUtf8Length>(); return;
case (1, true): state.WriteNull<TestStrictUtf8Token>(); return;
case (2, true): state.WriteNull<TestStrictUtf16Length>(); return;
case (3, true): state.WriteNull<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static bool ReadFor(this ref LuminPackJsonReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.Read<TestUtf8Length>();
case (1, false): return state.Read<TestUtf8Token>();
case (2, false): return state.Read<TestUtf16Length>();
case (3, false): return state.Read<TestUtf16Token>();
case (0, true): return state.Read<TestStrictUtf8Length>();
case (1, true): return state.Read<TestStrictUtf8Token>();
case (2, true): return state.Read<TestStrictUtf16Length>();
case (3, true): return state.Read<TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void SkipFor(this ref LuminPackJsonReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Skip<TestUtf8Length>(); return;
case (1, false): state.Skip<TestUtf8Token>(); return;
case (2, false): state.Skip<TestUtf16Length>(); return;
case (3, false): state.Skip<TestUtf16Token>(); return;
case (0, true): state.Skip<TestStrictUtf8Length>(); return;
case (1, true): state.Skip<TestStrictUtf8Token>(); return;
case (2, true): state.Skip<TestStrictUtf16Length>(); return;
case (3, true): state.Skip<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
internal static void EnsureEndOfDocumentFor(this ref LuminPackJsonReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.EnsureEndOfDocument<TestUtf8Length>(); return;
case (1, false): state.EnsureEndOfDocument<TestUtf8Token>(); return;
case (2, false): state.EnsureEndOfDocument<TestUtf16Length>(); return;
case (3, false): state.EnsureEndOfDocument<TestUtf16Token>(); return;
case (0, true): state.EnsureEndOfDocument<TestStrictUtf8Length>(); return;
case (1, true): state.EnsureEndOfDocument<TestStrictUtf8Token>(); return;
case (2, true): state.EnsureEndOfDocument<TestStrictUtf16Length>(); return;
case (3, true): state.EnsureEndOfDocument<TestStrictUtf16Token>(); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static float ReadNextFloatValueFor(this ref LuminPackJsonReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadNextFloatValue<TestUtf8Length>();
case (1, false): return state.ReadNextFloatValue<TestUtf8Token>();
case (2, false): return state.ReadNextFloatValue<TestUtf16Length>();
case (3, false): return state.ReadNextFloatValue<TestUtf16Token>();
case (0, true): return state.ReadNextFloatValue<TestStrictUtf8Length>();
case (1, true): return state.ReadNextFloatValue<TestStrictUtf8Token>();
case (2, true): return state.ReadNextFloatValue<TestStrictUtf16Length>();
case (3, true): return state.ReadNextFloatValue<TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <returns>The unchanged kernel result.</returns>
internal static double ReadDoubleFor(this ref LuminPackJsonReader state, TestSerializationProfile profile) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): return state.ReadDouble<TestUtf8Length>();
case (1, false): return state.ReadDouble<TestUtf8Token>();
case (2, false): return state.ReadDouble<TestUtf16Length>();
case (3, false): return state.ReadDouble<TestUtf16Token>();
case (0, true): return state.ReadDouble<TestStrictUtf8Length>();
case (1, true): return state.ReadDouble<TestStrictUtf8Token>();
case (2, true): return state.ReadDouble<TestStrictUtf16Length>();
case (3, true): return state.ReadDouble<TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
}

using System.Buffers;
using LuminPack.Core;
using LuminPack.Option;
namespace LuminPackUnitTest;
/// <summary>Selects closed kernels for parameterized encoding and grammar regressions.</summary>
internal static class CompiledProfileTestKernels {
/// <summary>Selects a statically compiled profile for one complete advancing operation.</summary>
/// <param name="state">The advancing binary state.</param>
/// <param name="profile">The encoding matrix row.</param>
/// <param name="value">The string storage used by the operation.</param>
internal static void WriteFor(this ref LuminPackWriter state, TestSerializationProfile profile, string? value) {
switch ((profile.StringEncoding, profile.StringRecording)) {
case (LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length): state.Write<LuminPackUtf8LengthOptions>(value); return;
case (LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token): state.Write<LuminPackUtf8TokenOptions>(value); return;
case (LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length): state.Write<LuminPackUtf16LengthOptions>(value); return;
case (LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token): state.Write<LuminPackUtf16TokenOptions>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Selects a statically compiled profile for one complete advancing operation.</summary>
/// <param name="state">The advancing binary state.</param>
/// <param name="profile">The encoding matrix row.</param>
/// <param name="current">The string storage used by the operation.</param>
/// <returns>The decoded or reused string.</returns>
internal static string? ReadFor(this ref LuminPackReader state, TestSerializationProfile profile, string? current = null) {
switch ((profile.StringEncoding, profile.StringRecording)) {
case (LuminPackStringEncoding.UTF8, LuminPackStringRecording.Length): return state.Read<LuminPackUtf8LengthOptions>(current);
case (LuminPackStringEncoding.UTF8, LuminPackStringRecording.Token): return state.Read<LuminPackUtf8TokenOptions>(current);
case (LuminPackStringEncoding.UTF16, LuminPackStringRecording.Length): return state.Read<LuminPackUtf16LengthOptions>(current);
case (LuminPackStringEncoding.UTF16, LuminPackStringRecording.Token): return state.Read<LuminPackUtf16TokenOptions>(current);
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
internal static void WriteFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, string? value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteFloatFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, float value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteDoubleFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, double value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteIntFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, int value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteLongFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, long value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
default: throw new InvalidOperationException("Invalid test profile."); } }
/// <summary>Exercises the original kernel with a statically closed test profile.</summary>
/// <param name="state">The original operation state.</param>
/// <param name="profile">The matrix row.</param>
/// <param name="value">The original kernel argument.</param>
internal static void WriteBoolFor(this ref LuminPackJsonWriter state, TestSerializationProfile profile, bool value) {
int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0); switch ((mode, profile.ValidateJsonSyntax)) {
case (0, false): state.Write<TestUtf8Length>(value); return;
case (1, false): state.Write<TestUtf8Token>(value); return;
case (2, false): state.Write<TestUtf16Length>(value); return;
case (3, false): state.Write<TestUtf16Token>(value); return;
case (0, true): state.Write<TestStrictUtf8Length>(value); return;
case (1, true): state.Write<TestStrictUtf8Token>(value); return;
case (2, true): state.Write<TestStrictUtf16Length>(value); return;
case (3, true): state.Write<TestStrictUtf16Token>(value); return;
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
case (0, false): return state.Read<double, TestUtf8Length>();
case (1, false): return state.Read<double, TestUtf8Token>();
case (2, false): return state.Read<double, TestUtf16Length>();
case (3, false): return state.Read<double, TestUtf16Token>();
case (0, true): return state.Read<double, TestStrictUtf8Length>();
case (1, true): return state.Read<double, TestStrictUtf8Token>();
case (2, true): return state.Read<double, TestStrictUtf16Length>();
case (3, true): return state.Read<double, TestStrictUtf16Token>();
default: throw new InvalidOperationException("Invalid test profile."); } }
}

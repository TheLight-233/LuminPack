using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Option
{
    /// <summary>Describes a test matrix row which selects a closed compile-time profile outside the production library.</summary>
    internal sealed record TestSerializationProfile
    {
        internal static readonly TestSerializationProfile Default = new();
        internal static readonly TestSerializationProfile Utf8 = new();
        internal static readonly TestSerializationProfile Utf16 = new() { StringEncoding = LuminPackStringEncoding.UTF16 };
        internal static readonly TestSerializationProfile Utf16WithLength = Utf16;
        internal static readonly TestSerializationProfile Token = new() { StringRecording = LuminPackStringRecording.Token };
        internal static readonly TestSerializationProfile Length = new();
        public LuminPackStringEncoding StringEncoding { get; set; }
        public LuminPackStringRecording StringRecording { get; set; }
        public StandardFormat StandardFormat { get; set; } = new('G');
        public int MaxJsonDepth { get; set; } = 1024;
        public bool ValidateJsonSyntax { get; set; }
    }
}

namespace LuminPack
{
    /// <summary>Selects closed generic profiles for the existing test matrix; production APIs have no runtime options.</summary>
    public static partial class LuminPackSerializer
    {
        /// <summary>Runs one test action with a statically closed profile selected by its matrix row.</summary>
        /// <typeparam name="TResult">The action result.</typeparam>
        /// <typeparam name="TAction">The value-type test action.</typeparam>
        /// <param name="action">The original test data and result storage.</param>
        /// <param name="profile">The test matrix row.</param>
        /// <returns>The result produced by the closed generated operation.</returns>
        private static TResult RunTestProfile<TResult, TAction>(ref TAction action, TestSerializationProfile? profile)
            where TAction : struct, IProfileTestAction<TResult>
        {
            if (profile == null) return action.Run<TestUtf8Length>();
            int mode = (profile.StringEncoding == LuminPackStringEncoding.UTF16 ? 2 : 0) | (profile.StringRecording == LuminPackStringRecording.Token ? 1 : 0);
            return (mode, profile.ValidateJsonSyntax) switch
            {
                (0, false) => action.Run<TestUtf8Length>(), (1, false) => action.Run<TestUtf8Token>(),
                (2, false) => action.Run<TestUtf16Length>(), (3, false) => action.Run<TestUtf16Token>(),
                (0, true) => action.Run<TestStrictUtf8Length>(), (1, true) => action.Run<TestStrictUtf8Token>(),
                (2, true) => action.Run<TestStrictUtf16Length>(), _ => action.Run<TestStrictUtf16Token>()
            };
        }

        /// <summary>Defines a test-only operation over a closed, stateless profile.</summary>
        /// <typeparam name="TResult">The operation result.</typeparam>
        private interface IProfileTestAction<TResult>
        {
            /// <summary>Executes the unchanged generated algorithm with the selected static profile.</summary>
            /// <typeparam name="TOptions">The selected stateless profile.</typeparam>
            /// <returns>The generated operation's result.</returns>
            TResult Run<TOptions>() where TOptions : struct, ILuminPackOptions;
        }

        /// <summary>Exercises binary serialization for one row of the former runtime-option test matrix.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="value">The original value.</param>
        /// <param name="profile">The matrix row selecting a closed generated profile.</param>
        /// <returns>The actual serialized bytes.</returns>
        internal static byte[] Serialize<T>(in T value, TestSerializationProfile? profile)
        {
            var action = new ProfileWrite<T> { Value = value };
            return RunTestProfile<byte[], ProfileWrite<T>>(ref action, profile);
        }

        /// <summary>Exercises a closed binary profile over a caller-owned reusable writer.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="value">The original value.</param>
        /// <param name="buffer">The existing reusable output.</param>
        /// <param name="profile">The matrix row.</param>
        internal static void Serialize<T>(in T value, LuminBufferWriter buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileWrite<T> { Value = value, Buffer = buffer };
            RunTestProfile<byte[], ProfileWrite<T>>(ref action, profile);
        }

        /// <summary>Exercises a closed binary profile against an existing test payload.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="buffer">The encoded payload.</param>
        /// <param name="profile">The matrix row.</param>
        /// <returns>The generated reader's result.</returns>
        internal static T Deserialize<T>(byte[] buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileRead<T> { Input = buffer, Value = default! };
            RunTestProfile<int, ProfileRead<T>>(ref action, profile);
            return action.Value;
        }

        /// <summary>Exercises a compiled profile over a borrowed input span.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="buffer">The original encoded bytes.</param>
        /// <param name="profile">The selected matrix row.</param>
        /// <returns>The decoded value.</returns>
        internal static T Deserialize<T>(ReadOnlySpan<byte> buffer, TestSerializationProfile? profile)
            => Deserialize<T>(buffer.ToArray(), profile);

        /// <summary>Exercises the production sequence-staging API with a closed profile.</summary>
        /// <typeparam name="T">The merge target type.</typeparam>
        /// <param name="buffer">The original segmented input.</param>
        /// <param name="value">The original merge target.</param>
        /// <param name="profile">The selected matrix row.</param>
        /// <returns>The actual consumed bytes.</returns>
        internal static int Deserialize<T>(in ReadOnlySequence<byte> buffer, ref T value, TestSerializationProfile? profile)
        {
            var action = new ProfileSequenceRead<T> { Input = buffer, Value = value };
            int consumed = RunTestProfile<int, ProfileSequenceRead<T>>(ref action, profile);
            value = action.Value;
            return consumed;
        }

        /// <summary>Exercises a closed reader profile while preserving the supplied merge target.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="buffer">The encoded payload.</param>
        /// <param name="value">The original merge storage.</param>
        /// <param name="profile">The matrix row.</param>
        /// <returns>The actual consumed bytes.</returns>
        internal static int Deserialize<T>(ReadOnlySpan<byte> buffer, ref T value, TestSerializationProfile? profile)
        {
            var action = new ProfileRead<T> { Input = buffer.ToArray(), Value = value };
            int consumed = RunTestProfile<int, ProfileRead<T>>(ref action, profile);
            value = action.Value;
            return consumed;
        }

        /// <summary>Exercises the JSON generator under a closed encoding and grammar profile.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="value">The original value.</param>
        /// <param name="profile">The matrix row.</param>
        /// <returns>The complete JSON text.</returns>
        internal static string SerializeJson<T>(T value, TestSerializationProfile? profile)
        {
            var action = new ProfileJsonWrite<T> { Value = value };
            return RunTestProfile<string, ProfileJsonWrite<T>>(ref action, profile);
        }

        /// <summary>Exercises the JSON generator over an existing reusable buffer.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="value">The original value.</param>
        /// <param name="buffer">The existing output.</param>
        /// <param name="profile">The matrix row.</param>
        internal static void SerializeJson<T>(T value, LuminBufferWriter buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileJsonWrite<T> { Value = value, Buffer = buffer };
            RunTestProfile<string, ProfileJsonWrite<T>>(ref action, profile);
        }

        /// <summary>Exercises the actual streaming JSON facade for a statically closed profile.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="value">The original payload.</param>
        /// <param name="buffer">The caller's streaming destination.</param>
        /// <param name="profile">The selected matrix row.</param>
        internal static void SerializeJson<T>(T value, IBufferWriter<byte> buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileJsonStream<T> { Value = value, Output = buffer };
            RunTestProfile<int, ProfileJsonStream<T>>(ref action, profile);
        }

        /// <summary>Exercises the JSON reader with an explicitly closed test profile.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="buffer">The complete JSON text.</param>
        /// <param name="profile">The matrix row.</param>
        /// <returns>The generated result.</returns>
        internal static T DeserializeJson<T>(string buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileJsonRead<T> { Input = JsonTestBytes(buffer.AsSpan(), profile), Value = default! };
            RunTestProfile<int, ProfileJsonRead<T>>(ref action, profile);
            return action.Value;
        }

        /// <summary>Exercises the production JSON reader over a borrowed byte range.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="buffer">The original document bytes.</param>
        /// <param name="profile">The selected matrix row.</param>
        /// <returns>The decoded value.</returns>
        internal static T DeserializeJson<T>(ReadOnlySpan<byte> buffer, TestSerializationProfile? profile)
        {
            var action = new ProfileJsonRead<T> { Input = buffer.ToArray(), Value = default! };
            RunTestProfile<int, ProfileJsonRead<T>>(ref action, profile);
            return action.Value;
        }

        /// <summary>Invokes the original segmented-input facade with a closed profile.</summary>
        /// <typeparam name="T">The merge target type.</typeparam>
        private struct ProfileSequenceRead<T> : IProfileTestAction<int>
        {
            internal ReadOnlySequence<byte> Input;
            internal T Value;
            /// <inheritdoc/>
            public int Run<TOptions>() where TOptions : struct, ILuminPackOptions
                => Deserialize<T, TOptions>(Input, ref Value);
        }

        /// <summary>Invokes the original streaming-output facade with a closed profile.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        private struct ProfileJsonStream<T> : IProfileTestAction<int>
        {
            internal T Value;
            internal IBufferWriter<byte> Output;
            /// <inheritdoc/>
            public int Run<TOptions>() where TOptions : struct, ILuminPackOptions
            { SerializeJson<T, TOptions>(Value, Output); return 0; }
        }

        /// <summary>Preserves raw UTF-16 input when selecting a closed JSON reader profile.</summary>
        /// <param name="input">The original text.</param>
        /// <param name="profile">The test matrix row.</param>
        /// <returns>The document bytes in the selected encoding.</returns>
        private static byte[] JsonTestBytes(ReadOnlySpan<char> input, TestSerializationProfile? profile)
            => profile?.StringEncoding == LuminPackStringEncoding.UTF16 ? MemoryMarshal.AsBytes(input).ToArray() : Encoding.UTF8.GetBytes(input.ToString());

        /// <summary>Preserves the original binary writer and its operation-state lifetime.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        private struct ProfileWrite<T> : IProfileTestAction<byte[]>
        {
            internal T Value;
            internal LuminBufferWriter? Buffer;
            /// <inheritdoc/>
            public byte[] Run<TOptions>() where TOptions : struct, ILuminPackOptions
{ if (Buffer == null) return Serialize<T, TOptions>(in Value); Serialize<T, TOptions>(in Value, Buffer); return Array.Empty<byte>(); }        }

        /// <summary>Preserves the original binary reader and merge storage.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        private struct ProfileRead<T> : IProfileTestAction<int>
        {
            internal byte[] Input;
            internal T Value;
            /// <inheritdoc/>
            public int Run<TOptions>() where TOptions : struct, ILuminPackOptions
{ return Deserialize<T, TOptions>(Input.AsSpan(), ref Value); }        }

        /// <summary>Preserves the original JSON writer algorithm and actual encoded-byte units.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        private struct ProfileJsonWrite<T> : IProfileTestAction<string>
        {
            internal T Value;
            internal LuminBufferWriter? Buffer;
            /// <inheritdoc/>
            public string Run<TOptions>() where TOptions : struct, ILuminPackOptions
{ if (Buffer == null) return SerializeJson<T, TOptions>(Value); SerializeJson<T, TOptions>(Value, Buffer); return string.Empty; }        }

        /// <summary>Preserves tokenizer initialization, root reads and complete-document validation.</summary>
        /// <typeparam name="T">The payload type.</typeparam>
        private struct ProfileJsonRead<T> : IProfileTestAction<int>
        {
            internal byte[] Input;
            internal T Value;
            /// <inheritdoc/>
            public int Run<TOptions>() where TOptions : struct, ILuminPackOptions
{ return DeserializeJson<T, TOptions>(Input.AsSpan(), ref Value); }        }
    }
}

namespace LuminPack.Option {
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF8, StringRecording=LuminPackStringRecording.Length, ValidateJsonSyntax=false)]
internal readonly struct TestUtf8Length : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => false; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF8, StringRecording=LuminPackStringRecording.Token, ValidateJsonSyntax=false)]
internal readonly struct TestUtf8Token : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => false; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF16, StringRecording=LuminPackStringRecording.Length, ValidateJsonSyntax=false)]
internal readonly struct TestUtf16Length : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => false; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF16, StringRecording=LuminPackStringRecording.Token, ValidateJsonSyntax=false)]
internal readonly struct TestUtf16Token : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => false; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF8, StringRecording=LuminPackStringRecording.Length, ValidateJsonSyntax=true)]
internal readonly struct TestStrictUtf8Length : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => true; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF8, StringRecording=LuminPackStringRecording.Token, ValidateJsonSyntax=true)]
internal readonly struct TestStrictUtf8Token : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => true; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF16, StringRecording=LuminPackStringRecording.Length, ValidateJsonSyntax=true)]
internal readonly struct TestStrictUtf16Length : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => true; }
/// <summary>Provides one statically closed row of the serialization test matrix.</summary>
[LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF16, StringRecording=LuminPackStringRecording.Token, ValidateJsonSyntax=true)]
internal readonly struct TestStrictUtf16Token : ILuminPackOptions {
/// <inheritdoc/>
public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
/// <inheritdoc/>
public LuminPackStringRecording StringRecording => LuminPackStringRecording.Token;
/// <inheritdoc/>
public StandardFormat StandardFormat => new('G');
/// <inheritdoc/>
public int MaxJsonDepth => 1024;
/// <inheritdoc/>
public bool ValidateJsonSyntax => true; }
}

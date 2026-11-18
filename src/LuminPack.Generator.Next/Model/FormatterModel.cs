using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Model;
/// <summary>The closed compile-time contract of one formatter and its static dependencies.</summary>
/// <remarks>Arguments describe element operations independently of the containing family. Layout describes contiguous storage,
/// which can differ from the element's standalone representation. Compiler symbols never leave analysis.</remarks>
internal sealed class FormatterModel : IEquatable<FormatterModel>
{
    /// <summary>The optional runtime capabilities proven against the consumer references.</summary>
    internal readonly string[] Capabilities;
    /// <summary>The canonical closed C# type.</summary>
    internal readonly string Type;
    /// <summary>The registered open definition, or an intrinsic contract name.</summary>
    internal readonly string Definition;
    /// <summary>The element and generic argument contracts, in declaration order.</summary>
    internal readonly FormatterModel[] Arguments;
    /// <summary>Whether the standalone binary operation is an unchecked reference-free value store.</summary>
    internal readonly bool Raw;
    /// <summary>Whether raw collection storage preserves the element's established contiguous contract.</summary>
    internal readonly bool RawCollection;
    /// <summary>Whether an element loop can use checked native storage without a layout tag.</summary>
    /// <remarks>The one-model CLR layouts of small scalar nullables have the same flag and value offsets on the
    /// supported 32-bit and 64-bit runtimes. Compound Tagged values need a descriptor to prove their local layout.</remarks>
    internal bool NativeNullableElement => NullableEncoding == 2 ||
        NullableEncoding == 1 && NullableStorage is { ModelSizes.Length: 1 } &&
        (Arguments[0].Underlying ?? Arguments[0].Type) is "bool" or "byte" or "sbyte" or "char" or "short" or "ushort" or "int" or "uint" or "float";
    /// <summary>Whether the value's own formatter is exactly a copy of its native storage, so a keyed entry may store it raw.</summary>
    /// <remarks>The original raw-storage rule for dictionary keys and values: a contiguous collection copies any reference-free
    /// element, but a pair, a version-tolerant or managed-record struct and a Union frame their values themselves.</remarks>
    internal bool RawStorage => RawCollection && Definition != Rendering.Formatters.GenericIdentity.KeyValuePair && Payload is not UnionModel &&
        Payload is not StructModel { Versioned: true } && Payload is not StructModel { Storage: StructStorage.ManagedRecord };

    /// <summary>The payload of a marker-prefixed nullable whose own binary value uses native storage.</summary>
    internal string? NativeNullablePayload => Definition == Rendering.Formatters.GenericIdentity.Nullable && Arguments.Length == 1 &&
        Arguments[0] is { Raw: true, RawStorage: true, Layout: null } argument ? argument.Type : null;

    /// <summary>The optional packed or tagged contiguous storage plan.</summary>
    internal readonly FormatterStorageModel? Layout;
    /// <summary>The proven layout of a nullable collection element.</summary>
    internal readonly FormatterStorageModel? NullableStorage;
    /// <summary>The nullable collection element encoding: zero for framed, one for tagged, and two for native.</summary>
    internal readonly int NullableEncoding;
    /// <summary>Whether the compiler proves that a nullable's underlying type is unmanaged.</summary>
    internal readonly bool UnmanagedNullablePayload;
    /// <summary>Whether shared publication must emit a payload outside the attributed declaration stream.</summary>
    internal readonly bool PublishPayload;
    /// <summary>The analyzed payload when this compilation owns its formatter.</summary>
    internal readonly TypeModel? Payload;
    /// <summary>The enum's primitive owner identity, or null.</summary>
    internal readonly string? Underlying;
    /// <summary>Whether an element null marker must overwrite an existing ordinary object.</summary>
    internal readonly bool OrdinaryObject;
    /// <summary>The array rank, or zero for a named type.</summary>
    internal readonly int Rank;
    /// <summary>Whether the binary array path preserves an existing destination or uses string-matrix slots.</summary>
    internal readonly bool ReuseArraySlots;
    /// <summary>The optional framework formatted-scalar JSON capability.</summary>
    internal readonly bool FormattedJson;
    /// <summary>Captures one normalized formatter contract.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "definition">The definition used by this operation.</param>
    /// <param name = "arguments">The arguments used by this operation.</param>
    /// <param name = "raw">The raw used by this operation.</param>
    /// <param name = "rawCollection">The raw collection used by this operation.</param>
    /// <param name = "layout">The layout used by this operation.</param>
    /// <param name = "payload">The payload used by this operation.</param>
    /// <param name = "underlying">The underlying used by this operation.</param>
    /// <param name = "ordinaryObject">The ordinary object used by this operation.</param>
    /// <param name = "rank">The rank used by this operation.</param>
    /// <param name = "formattedJson">The formatted json used by this operation.</param>
    /// <param name = "reuseArraySlots">The reuse array slots used by this operation.</param>
    /// <param name = "capabilities">The capabilities used by this operation.</param>
    /// <param name = "publishPayload">The publish payload used by this operation.</param>
    /// <param name = "nullableStorage">The verified block models of the nullable element.</param>
    /// <param name = "nullableEncoding">The binary nullable collection element encoding.</param>
    /// <param name = "unmanagedNullablePayload">The compiler's unmanaged proof for the underlying nullable type.</param>
    internal FormatterModel(string type, string definition, FormatterModel[]? arguments = null, bool raw = false, bool rawCollection = false, FormatterStorageModel? layout = null, TypeModel? payload = null, string? underlying = null, bool ordinaryObject = false, int rank = 0, bool formattedJson = false, bool reuseArraySlots = false, string[]? capabilities = null, bool publishPayload = false, FormatterStorageModel? nullableStorage = null, int nullableEncoding = 0, bool unmanagedNullablePayload = false)
    {
        PublishPayload = publishPayload;
        Capabilities = capabilities ?? Array.Empty<string>();
        Type = type;
        Definition = definition;
        Arguments = arguments ?? Array.Empty<FormatterModel>();
        Raw = raw;
        RawCollection = rawCollection;
        Layout = layout;
        NullableStorage = nullableStorage;
        NullableEncoding = nullableEncoding;
        UnmanagedNullablePayload = unmanagedNullablePayload;
        Payload = payload;
        Underlying = underlying;
        OrdinaryObject = ordinaryObject;
        Rank = rank;
        ReuseArraySlots = reuseArraySlots;
        FormattedJson = formattedJson;
    }

    /// <summary>Checks an optional operation without encoding type families in a flags enum.</summary>
    /// <param name = "capability">The capability used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    internal bool Supports(string capability) => Array.IndexOf(Capabilities, capability) >= 0;
    /// <summary>Attaches the immutable runtime API facts captured by analysis.</summary>
    /// <param name = "capabilities">The capabilities used by this operation.</param>
    /// <returns>The normalized result of the operation.</returns>
    internal FormatterModel WithCapabilities(string[] capabilities) => capabilities.Length == 0 ? this : new FormatterModel(Type, Definition, Arguments, Raw, RawCollection, Layout, Payload, Underlying, OrdinaryObject, Rank, FormattedJson, ReuseArraySlots, capabilities, PublishPayload, NullableStorage, NullableEncoding, UnmanagedNullablePayload);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "other">The other used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public bool Equals(FormatterModel? other) => ReferenceEquals(this, other) || other != null && Type == other.Type && Definition == other.Definition && Raw == other.Raw && RawCollection == other.RawCollection && OrdinaryObject == other.OrdinaryObject && ReuseArraySlots == other.ReuseArraySlots && Rank == other.Rank && Underlying == other.Underlying && FormattedJson == other.FormattedJson && PublishPayload == other.PublishPayload && Equals(Layout, other.Layout) && Equals(NullableStorage, other.NullableStorage) && NullableEncoding == other.NullableEncoding && UnmanagedNullablePayload == other.UnmanagedNullablePayload && Equals(Payload, other.Payload) && Arguments.SequenceEqual(other.Arguments) && Capabilities.SequenceEqual(other.Capabilities);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "obj">The obj used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public override bool Equals(object? obj) => Equals(obj as FormatterModel);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <returns>The normalized result of the operation.</returns>
    public override int GetHashCode() => Type.GetHashCode();
}

/// <summary>A recursive ABI storage description used by packed and tagged formatter operations.</summary>
internal sealed class FormatterStorageModel : IEquatable<FormatterStorageModel>
{
    /// <summary>The CLR storage type.</summary>
    internal readonly string Type;
    /// <summary>The ABI-preserving layout attribute of the storage mirror.</summary>
    internal readonly string? Attribute;
    /// <summary>Whether this node is an aggregate, including an empty struct.</summary>
    internal readonly bool Aggregate;
    /// <summary>Whether physical fields are directly accessible tuple components.</summary>
    internal readonly bool Direct;
    /// <summary>The storage fields, empty for scalar leaves.</summary>
    internal readonly FormatterStorageField[] Fields;
    /// <summary>The packed byte width.</summary>
    internal readonly int WireSize;
    /// <summary>Whether a contiguous nonempty payload carries its native-layout tag.</summary>
    internal readonly bool Tagged;
    /// <summary>Whether any nested field requires volatile scalar access.</summary>
    internal readonly bool Volatile;
    /// <summary>The native model widths selected by the one-based wire tag.</summary>
    internal readonly int[] ModelSizes;
    /// <summary>The native byte offsets for every model, in packed field order.</summary>
    internal readonly int[][] ModelOffsets;
    /// <summary>The payload type when the descriptor verifies a nullable's actual payload field address.</summary>
    internal readonly string? NullablePayload;
    /// <summary>Captures the storage facts after compiler analysis.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "attribute">The attribute used by this operation.</param>
    /// <param name = "fields">The fields used by this operation.</param>
    /// <param name = "wireSize">The wire size used by this operation.</param>
    /// <param name = "tagged">The tagged used by this operation.</param>
    /// <param name = "isVolatile">The is volatile used by this operation.</param>
    /// <param name = "modelSizes">The model sizes used by this operation.</param>
    /// <param name = "modelOffsets">The model offsets used by this operation.</param>
    /// <param name = "direct">The direct used by this operation.</param>
    /// <param name = "aggregate">The aggregate used by this operation.</param>
    /// <param name = "nullablePayload">The underlying type of an explicitly modeled nullable wrapper.</param>
    internal FormatterStorageModel(string type, string? attribute, FormatterStorageField[] fields, int wireSize, bool tagged, bool isVolatile, int[] modelSizes, int[][] modelOffsets, bool direct = false, bool aggregate = false, string? nullablePayload = null)
    {
        Aggregate = aggregate;
        Direct = direct;
        Type = type;
        Attribute = attribute;
        Fields = fields;
        WireSize = wireSize;
        Tagged = tagged;
        Volatile = isVolatile;
        ModelSizes = modelSizes;
        ModelOffsets = modelOffsets;
        NullablePayload = nullablePayload;
    }

    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "other">The other used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public bool Equals(FormatterStorageModel? other) => ReferenceEquals(this, other) || other != null && Type == other.Type && Attribute == other.Attribute && Aggregate == other.Aggregate && Direct == other.Direct && WireSize == other.WireSize && Tagged == other.Tagged && Volatile == other.Volatile && NullablePayload == other.NullablePayload && Fields.SequenceEqual(other.Fields) && ModelSizes.SequenceEqual(other.ModelSizes) && ModelOffsets.Length == other.ModelOffsets.Length && ModelOffsets.Zip(other.ModelOffsets, (a, b) => a.SequenceEqual(b)).All(x => x);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "obj">The obj used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public override bool Equals(object? obj) => Equals(obj as FormatterStorageModel);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <returns>The normalized result of the operation.</returns>
    public override int GetHashCode() => Type.GetHashCode();
}

/// <summary>One physical field of an unmanaged storage mirror.</summary>
internal sealed class FormatterStorageField : IEquatable<FormatterStorageField>
{
    /// <summary>The physical field name of a directly accessed tuple component.</summary>
    internal readonly string Name;
    /// <summary>The nested field layout.</summary>
    internal readonly FormatterStorageModel Storage;
    /// <summary>The explicit CLR offset, or a negative value for sequential or automatic storage.</summary>
    internal readonly int Offset;
    /// <summary>Whether accesses to this field are volatile.</summary>
    internal readonly bool Volatile;
    /// <summary>Captures one field.</summary>
    /// <param name = "storage">The storage used by this operation.</param>
    /// <param name = "offset">The offset used by this operation.</param>
    /// <param name = "isVolatile">The is volatile used by this operation.</param>
    /// <param name = "name">The name used by this operation.</param>
    internal FormatterStorageField(FormatterStorageModel storage, int offset, bool isVolatile, string name)
    {
        Name = name;
        Storage = storage;
        Offset = offset;
        Volatile = isVolatile;
    }

    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "other">The other used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public bool Equals(FormatterStorageField? other) => other != null && Name == other.Name && Storage.Equals(other.Storage) && Offset == other.Offset && Volatile == other.Volatile;
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <param name = "obj">The obj used by this operation.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    public override bool Equals(object? obj) => Equals(obj as FormatterStorageField);
    /// <inheritdoc/>
    /// <summary>Processes the current formatter contract.</summary>
    /// <returns>The normalized result of the operation.</returns>
    public override int GetHashCode() => Storage.GetHashCode() ^ Offset;
}

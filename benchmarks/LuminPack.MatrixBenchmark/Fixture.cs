using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MemoryPack;

internal delegate void ReadValue<T, TPayload>(TPayload payload, ref T value);

internal abstract class Fixture(string name)
{
    internal string Name { get; } = name;
    /// <summary>Prepares LuminPack and reference-library operations over the same fixture values.</summary>
    /// <param name="json">Whether the selected format is JSON.</param>
    /// <param name="changing">Whether to alternate between two distinct input values.</param>
    /// <returns>The codecs and any unavailable comparison entries for the selected format.</returns>
    internal abstract IEnumerable<Codec> Prepare(bool json, bool changing);

    /// <summary>Creates a fixture with concrete LuminPack binary and JSON delegates.</summary>
    /// <typeparam name="T">The payload type under test.</typeparam>
    /// <param name="name">The fixture name used by the report.</param>
    /// <param name="create">Creates an input value from a deterministic seed.</param>
    /// <param name="write">Serializes a value to binary.</param>
    /// <param name="read">Deserializes binary into an existing destination.</param>
    /// <param name="writeJson">Serializes a value to JSON.</param>
    /// <param name="readJson">Deserializes JSON into an existing destination.</param>
    /// <param name="snapshot">Optionally normalizes values for round-trip comparison.</param>
    /// <returns>The fixture used to prepare each library's operations.</returns>
    internal static Fixture Create<T>(string name, Func<int, T> create,
        Func<T, byte[]> write, ReadValue<T, byte[]> read,
        Func<T, string> writeJson, ReadValue<T, string> readJson,
        Func<T, string>? snapshot = null)
        => new TypedFixture<T>(name, create, write, read, writeJson, readJson,
            snapshot ?? (value => Canonical.Of(value)));

    private sealed class TypedFixture<T>(string name, Func<int, T> create,
        Func<T, byte[]> write, ReadValue<T, byte[]> read,
        Func<T, string> writeJson, ReadValue<T, string> readJson,
        Func<T, string> snapshot) : Fixture(name)
    {
        /// <inheritdoc/>
        internal override IEnumerable<Codec> Prepare(bool json, bool changing)
        {
            T first = create(0);
            T second = changing ? create(100_000) : first;
            string expected = snapshot(first);
            string changed = snapshot(second);

            if (json)
            {
                yield return new TypedCodec<T, string>("LuminPack", first, second, expected, changed,
                    snapshot, writeJson, readJson, Encoding.UTF8.GetByteCount);
                var metadata = MatrixJsonContext.Default.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>;
                if (metadata is null)
                {
                    yield return new UnsupportedCodec("System.Text.Json", "Generated JSON metadata is unavailable.");
                    yield break;
                }

                yield return new TypedCodec<T, string>("System.Text.Json", first, second, expected, changed,
                    snapshot, value => JsonSerializer.Serialize(value, metadata),
                    (string payload, ref T value) => value = JsonSerializer.Deserialize(payload, metadata)!,
                    Encoding.UTF8.GetByteCount, reuseUnavailable: "No existing-root API.");
            }
            else
            {
                yield return new TypedCodec<T, byte[]>("LuminPack", first, second, expected, changed,
                    snapshot, write, read, static payload => payload.Length);
                // Repeated reads can terminate the process before managed exception handling runs.
                if (typeof(T) == typeof(BitArray))
                {
                    yield return new UnsupportedCodec("MemoryPack", "Repeated BitArray reads with MemoryPack 1.21.4 caused an access violation on .NET 10.");
                    yield break;
                }

                yield return new TypedCodec<T, byte[]>("MemoryPack", first, second, expected, changed,
                    snapshot, static value => MemoryPackSerializer.Serialize(value), ReadMemoryPack,
                    static payload => payload.Length);
            }
        }

        /// <summary>Deserializes a MemoryPack record into an existing destination.</summary>
        /// <param name="payload">The serialized MemoryPack bytes.</param>
        /// <param name="value">The destination value, initially empty or populated.</param>
        private static void ReadMemoryPack(byte[] payload, ref T value)
            => MemoryPackSerializer.Deserialize(payload.AsSpan(), ref value);
    }
}

internal abstract class Codec(string library)
{
    internal string Library { get; } = library;
    internal abstract bool SupportsReuse { get; }
    internal virtual string? ReuseUnavailable => null;
    /// <summary>Checks the codec's supported round trips before it is timed.</summary>
    /// <returns>The serialized size in bytes of the first fixture value.</returns>
    internal abstract int Validate();
    internal abstract Action Write { get; }
    internal abstract Action Fresh { get; }
    internal abstract Action Reuse { get; }
}

internal sealed class UnsupportedCodec(string library, string reason) : Codec(library)
{
    internal override bool SupportsReuse => false;
    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">The reference library cannot run this fixture.</exception>
    internal override int Validate() => throw new NotSupportedException(reason);
    internal override Action Write => throw new NotSupportedException(reason);
    internal override Action Fresh => throw new NotSupportedException(reason);
    internal override Action Reuse => throw new NotSupportedException(reason);
}

internal sealed class TypedCodec<T, TPayload>(
    string library, T first, T second, string expected, string changed, Func<T, string> snapshot,
    Func<T, TPayload> write, ReadValue<T, TPayload> read, Func<TPayload, int> size, string? reuseUnavailable = null)
    : Codec(library)
{
    private TPayload firstPayload = default!;
    private TPayload secondPayload = default!;
    private T reused = default!;
    private T fresh = default!;
    private TPayload written = default!;
    private int iteration;

    internal override bool SupportsReuse => reuseUnavailable is null;
    internal override string? ReuseUnavailable => reuseUnavailable;
    internal override Action Write => WriteNext;
    internal override Action Fresh => ReadFresh;
    internal override Action Reuse => ReadReused;

    /// <inheritdoc/>
    internal override int Validate()
    {
        firstPayload = write(first);
        secondPayload = write(second);
        T firstValue = default!;
        T secondValue = default!;
        read(firstPayload, ref firstValue);
        read(secondPayload, ref secondValue);
        if (snapshot(firstValue) != expected || snapshot(secondValue) != changed)
            throw new InvalidOperationException("Fresh round trip changed the fixture.");

        if (SupportsReuse)
        {
            reused = firstValue;
            read(secondPayload, ref reused);
            if (snapshot(reused) != changed)
                throw new InvalidOperationException("Reuse did not apply the second input.");
            read(firstPayload, ref reused);
            if (snapshot(reused) != expected)
                throw new InvalidOperationException("Reuse did not restore the first input.");
        }

        return size(firstPayload);
    }

    /// <summary>Alternates the value used by successive benchmark calls.</summary>
    /// <returns>Whether the next call uses the first fixture value.</returns>
    private bool UseFirst() => (++iteration & 1) != 0;

    /// <summary>Serializes the next fixture value to a new payload.</summary>
    private void WriteNext() => written = write(UseFirst() ? first : second);

    /// <summary>Deserializes the next payload into a default destination.</summary>
    private void ReadFresh()
    {
        T value = default!;
        read(UseFirst() ? firstPayload : secondPayload, ref value);
        fresh = value;
    }

    /// <summary>Deserializes the next payload into the retained destination.</summary>
    private void ReadReused() => read(UseFirst() ? firstPayload : secondPayload, ref reused);
}

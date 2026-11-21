#if LUMINPACK_NEXT_WIRE
using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPack.WireCompatibility
{
    internal static partial class WireFixtures
    {
        /// <summary>Writes the established JSON reference envelope for a cycle.</summary>
        /// <returns>The first occurrence and its back reference.</returns>
        private static string ReferenceJson()
        {
            var value = new Tracked { Value = 37 };
            value.Next = value;
            return LuminPackSerializer.SerializeJson(value);
        }

        /// <summary>Reads the retained reference envelope and verifies its identity.</summary>
        /// <param name="directory">The fixture directory.</param>
        private static void VerifyReferenceJson(string directory)
        {
            Tracked? copy = null;
            LuminPackSerializer.DeserializeJson(File.ReadAllText(Path.Combine(directory, "references.json")), ref copy);
            Check(copy?.Value == 37 && ReferenceEquals(copy, copy.Next), "references.json");
        }

        /// <summary>Writes complete, short, scoped, evolved and reference-tracked version fixtures.</summary>
        /// <param name="directory">The destination fixture directory.</param>
        private static void WriteVersionFixtures(string directory)
        {
            var original = new VersionOne { Id = 13, Name = "player", Removed = 17 };
            var evolved = new VersionTwo { Id = 19, DisplayName = "renamed", Added = 23 };
            var second = new VersionOne { Id = 29, Name = "next", Removed = 31 };
            var trackedObject = new WireTrackedObject { Value = 37 };
            trackedObject.Next = trackedObject;
            var trackedVersion = new WireTrackedVersion { Value = 41 };
            trackedVersion.Next = trackedVersion;
            VersionOne[] collection = [original, second];
            var scopes = new WireVersionScopes { First = collection, Second = collection };
            File.WriteAllBytes(Path.Combine(directory, "version-complete.bin"), LuminPackSerializer.Serialize(original));
            File.WriteAllBytes(Path.Combine(directory, "version-short-collection.bin"), LuminPackSerializer.Serialize(collection));
            File.WriteAllBytes(Path.Combine(directory, "version-scopes.bin"), LuminPackSerializer.Serialize(scopes));
            File.WriteAllBytes(Path.Combine(directory, "version-evolved.bin"), LuminPackSerializer.Serialize(evolved));
            File.WriteAllBytes(Path.Combine(directory, "references-object.bin"), LuminPackSerializer.Serialize(trackedObject));
            File.WriteAllBytes(Path.Combine(directory, "references-version.bin"), LuminPackSerializer.Serialize(trackedVersion));
        }

        /// <summary>Reads the retained fixtures and checks compatibility, record form and registration scope.</summary>
        /// <param name="directory">The retained fixture directory.</param>
        private static void VerifyVersionFixtures(string directory)
        {
            byte[] complete = File.ReadAllBytes(Path.Combine(directory, "version-complete.bin"));
            VersionTwo? renamed = null;
            Check(LuminPackSerializer.Deserialize(complete, ref renamed) == complete.Length &&
                renamed is { Id: 13, DisplayName: "player", Added: 0 }, "version-complete.bin");
            Check(complete[0] == 251 && complete[1] == 0x42 && complete[2] == 3, "version-complete.bin header");

            byte[] evolved = File.ReadAllBytes(Path.Combine(directory, "version-evolved.bin"));
            VersionOne? older = null;
            Check(LuminPackSerializer.Deserialize(evolved, ref older) == evolved.Length &&
                older is { Id: 19, Name: "renamed", Removed: 0 }, "version-evolved.bin");
            VerifyIncompatibleVersion(complete);

            byte[] collection = File.ReadAllBytes(Path.Combine(directory, "version-short-collection.bin"));
            VersionOne[]? items = null;
            Check(LuminPackSerializer.Deserialize(collection, ref items) == collection.Length &&
                items is [{ Id: 13 }, { Id: 29 }], "version-short-collection.bin");
            Check(collection.AsSpan(4, complete.Length).SequenceEqual(complete) &&
                collection[4 + complete.Length] == 251 && collection[5 + complete.Length] == 0x52 &&
                collection[6 + complete.Length] == 0, "version-short-collection.bin registrations");

            byte[] scopes = File.ReadAllBytes(Path.Combine(directory, "version-scopes.bin"));
            WireVersionScopes? scoped = null;
            Check(LuminPackSerializer.Deserialize(scopes, ref scoped) == scopes.Length &&
                scoped is { First: [{ Id: 13 }, { Id: 29 }], Second: [{ Id: 13 }, { Id: 29 }] }, "version-scopes.bin");
            const int firstLengthOffset = 11 + 2 * 8;
            int firstLength = BinaryPrimitives.ReadInt32LittleEndian(scopes.AsSpan(firstLengthOffset));
            // The root registration occupies ordinal zero, so both scopes assign ordinal one to the collection element.
            collection[6 + complete.Length] = 1;
            Check(scopes.AsSpan(firstLengthOffset + 4, firstLength).SequenceEqual(collection), "version-scopes.bin first scope");
            int secondLengthOffset = firstLengthOffset + 4 + firstLength;
            int secondLength = BinaryPrimitives.ReadInt32LittleEndian(scopes.AsSpan(secondLengthOffset));
            Check(scopes.AsSpan(secondLengthOffset + 4, secondLength)
                .SequenceEqual(scopes.AsSpan(firstLengthOffset + 4, firstLength)), "version-scopes.bin second scope");

            VerifyReferenceBodies(directory);
            byte[] legacy = File.ReadAllBytes(Path.Combine(directory, "version-revision-1.bin"));
            CompatVersion? original = null;
            Check(LuminPackSerializer.Deserialize(legacy, ref original) == legacy.Length &&
                original is { Id: 7, Name: "name", Values: [1, 2, 3], Child: { Flag: true, Stamp: 99 } },
                "version-revision-1.bin");
        }

        /// <summary>Requires a type change with a matching member name to fail before publication.</summary>
        /// <param name="bytes">The incompatible version record.</param>
        private static void VerifyIncompatibleVersion(byte[] bytes)
        {
            WireVersionConflict? target = null;
            try
            {
                LuminPackSerializer.Deserialize(bytes, ref target);
            }
            catch (FormatException)
            {
                Check(target is null, "incompatible version publication");
                return;
            }
            throw new InvalidOperationException("Wire fixture accepted an incompatible version field.");
        }

        /// <summary>Verifies the same reference head composes with both record bodies and preserves identity.</summary>
        /// <param name="directory">The retained fixture directory.</param>
        private static void VerifyReferenceBodies(string directory)
        {
            byte[] positional = File.ReadAllBytes(Path.Combine(directory, "references-object.bin"));
            WireTrackedObject? positionalCopy = null;
            Check(LuminPackSerializer.Deserialize(positional, ref positionalCopy) == positional.Length &&
                positionalCopy?.Value == 37 && ReferenceEquals(positionalCopy, positionalCopy.Next), "references-object.bin");
            Check(positional.AsSpan(0, 3).SequenceEqual(new byte[] { 2, 0, 2 }) &&
                positional.AsSpan(^2).SequenceEqual(new byte[] { 250, 0 }), "references-object.bin framing");

            byte[] versioned = File.ReadAllBytes(Path.Combine(directory, "references-version.bin"));
            WireTrackedVersion? versionedCopy = null;
            Check(LuminPackSerializer.Deserialize(versioned, ref versionedCopy) == versioned.Length &&
                versionedCopy?.Value == 41 && ReferenceEquals(versionedCopy, versionedCopy.Next), "references-version.bin");
            Check(versioned.AsSpan(0, 5).SequenceEqual(new byte[] { 2, 0, 251, 0x42, 2 }) &&
                versioned.AsSpan(^6).SequenceEqual(new byte[] { 2, 0, 0, 0, 250, 0 }), "references-version.bin framing");
        }
    }

    /// <summary>Stores the same element schema in two independent length-prefixed scopes.</summary>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
    public sealed class WireVersionScopes { public VersionOne[] First = []; public VersionOne[] Second = []; }

    /// <summary>Replaces the sender's Int32 member with a Single of equal width.</summary>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
    public sealed class WireVersionConflict { public float Id; }

    /// <summary>Uses positional records inside the reference framing.</summary>
    [LuminPackable, LuminPackOption(TrackReferences = true)]
    public sealed class WireTrackedObject { public int Value; public WireTrackedObject? Next; }

    /// <summary>Uses version records inside the reference framing.</summary>
    [LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
    public sealed class WireTrackedVersion { public int Value; public WireTrackedVersion? Next; }
}

/// <summary>Provides the exact global type names stored in the release revision-1 fixture.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class CompatVersion { public int Id; public string Name = ""; public int[] Values = []; public CompatVersionChild? Child; }

/// <summary>Provides the nested revision-1 fixture type.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public sealed class CompatVersionChild { public bool Flag; public long Stamp; }
#endif

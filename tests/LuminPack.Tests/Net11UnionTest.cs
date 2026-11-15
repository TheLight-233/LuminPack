#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    /// <summary>Provides union cat functionality.</summary>
    [LuminPackable]
    public partial class UnionCat
    {
        /// <summary>Stores the id.</summary>
        public int Id;
    }

    /// <summary>Provides union dog functionality.</summary>
    [LuminPackable]
    public partial class UnionDog
    {
        /// <summary>Stores the name.</summary>
        public string Name = "";
    }

    // C# 15 / .NET 11 union declaration.  The generator auto-recognizes it as a LuminPack union
    // root and dispatches on the case type (same wire format as [LuminPackUnion] class unions).
    /// <summary>Provides net11 pet functionality.</summary>
    [LuminPackable]
    public union Net11Pet(UnionCat, UnionDog);

    /// <summary>Provides some functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public partial class Some<T>
    {
        /// <summary>Stores the data.</summary>
        public T Data = default!;
    }

    /// <summary>Provides none functionality.</summary>
    [LuminPackable]
    public partial class None
    {
    }

    /// <summary>Provides net11 option functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public union Net11Option<T>(None, Some<T>);

    /// <summary>Provides net11 maybe functionality.</summary>
    [LuminPackable]
    public union Net11Maybe(int, string);

    // Existing abstract-class polymorphism must keep working on .NET 11.
    /// <summary>Provides net11 animal functionality.</summary>
    [LuminPackable]
    public abstract partial class Net11Animal
    {
        /// <summary>Stores the legs.</summary>
        public int Legs;
    }

    /// <summary>Provides net11 dog functionality.</summary>
    [LuminPackable]
    public sealed partial class Net11Dog : Net11Animal
    {
        /// <summary>Stores the bark.</summary>
        public string Bark = "";
    }

    /// <summary>Provides net11 cat functionality.</summary>
    [LuminPackable]
    public sealed partial class Net11Cat : Net11Animal
    {
        /// <summary>Stores the meow.</summary>
        public string Meow = "";
    }

    /// <summary>Provides net11 union test functionality.</summary>
    public static class Net11UnionTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void Run(List<string> results)
        {
            // C# union binary round-trip
            try
            {
                var p = new Net11Pet(new UnionCat { Id = 7 });
                byte[] bytes = LuminPackSerializer.Serialize(p);
                Net11Pet back = default!;
                LuminPackSerializer.Deserialize(bytes, ref back);
                results.Add(back.Value is UnionCat c && c.Id == 7
                    ? "✓ Net11Union C# union - PASSED"
                    : "✗ Net11Union C# union - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union C# union - ERROR: {ex.Message}");
            }

            // C# union JSON round-trip
            try
            {
                var p = new Net11Pet(new UnionDog { Name = "doggie" });
                string json = LuminPackSerializer.SerializeJson(p);
                Net11Pet back = default!;
                LuminPackSerializer.DeserializeJson(json, ref back);
                results.Add(back.Value is UnionDog d && d.Name == "doggie"
                    ? "✓ Net11Union C# union JSON - PASSED"
                    : "✗ Net11Union C# union JSON - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union C# union JSON - ERROR: {ex.Message}");
            }

            // Generic union Option<T>
            try
            {
                var opt = new Net11Option<UnionCat>(new Some<UnionCat> { Data = new UnionCat { Id = 5 } });
                byte[] bytes = LuminPackSerializer.Serialize(opt);
                Net11Option<UnionCat> back = default!;
                LuminPackSerializer.Deserialize(bytes, ref back);
                results.Add(back.Value is Some<UnionCat> s && s.Data.Id == 5
                    ? "✓ Net11Union generic Option<T> - PASSED"
                    : "✗ Net11Union generic Option<T> - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union generic Option<T> - ERROR: {ex.Message}");
            }

            // Value-type case types
            try
            {
                var m = new Net11Maybe(42);
                byte[] bytes = LuminPackSerializer.Serialize(m);
                Net11Maybe back = default!;
                LuminPackSerializer.Deserialize(bytes, ref back);
                string json = LuminPackSerializer.SerializeJson(m);
                results.Add(back.Value is int i && i == 42 &&
                    bytes.AsSpan().SequenceEqual(new byte[] { 0, 42, 0, 0, 0 }) &&
                    json == "{\"$type\":0,\"$value\":42}"
                    ? "✓ Net11Union value-type case - PASSED"
                    : "✗ Net11Union value-type case - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union value-type case - ERROR: {ex.Message}");
            }

            try
            {
                Net11Maybe empty = default;
                byte[] bytes = LuminPackSerializer.Serialize(empty);
                string json = LuminPackSerializer.SerializeJson(empty);
                var binaryReuse = new Net11Maybe(42);
                var jsonReuse = new Net11Maybe("previous value");
                LuminPackSerializer.Deserialize(bytes, ref binaryReuse);
                LuminPackSerializer.DeserializeJson(json, ref jsonReuse);
                results.Add(bytes.AsSpan().SequenceEqual(new byte[] { 255 }) && json == "null" &&
                    binaryReuse.Value is null && jsonReuse.Value is null
                    ? "✓ Net11Union empty wire and reuse - PASSED"
                    : "✗ Net11Union empty wire and reuse - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union empty wire and reuse - ERROR: {ex.Message}");
            }

#if LUMINPACK_NEXT_GENERATOR
            try
            {
                Net11Maybe[] values = [new Net11Maybe(42), new Net11Maybe("text"), default];
                byte[] bytes = LuminPackSerializer.Serialize(values);
                string json = LuminPackSerializer.SerializeJson(values);
                Net11Maybe[] binaryCopy = null!;
                Net11Maybe[] jsonCopy = null!;
                LuminPackSerializer.Deserialize(bytes, ref binaryCopy);
                LuminPackSerializer.DeserializeJson(json, ref jsonCopy);
                results.Add(binaryCopy.Length == 3 && jsonCopy.Length == 3 &&
                    binaryCopy[0].Value is 42 && jsonCopy[0].Value is 42 &&
                    binaryCopy[1].Value is "text" && jsonCopy[1].Value is "text" &&
                    binaryCopy[2].Value is null && jsonCopy[2].Value is null
                    ? "✓ Net11Union array cases - PASSED"
                    : "✗ Net11Union array cases - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union array cases - ERROR: {ex.Message}");
            }
#endif

            // Abstract-class polymorphism (existing mechanism) still works on .NET 11
            try
            {
                Net11Animal animal = new Net11Dog { Legs = 4, Bark = "woof" };
                byte[] bytes = LuminPackSerializer.Serialize(animal);
                Net11Animal back = null!;
                LuminPackSerializer.Deserialize(bytes, ref back);
                results.Add(back is Net11Dog d && d.Bark == "woof"
                    ? "✓ Net11Union abstract-class polymorphism - PASSED"
                    : "✗ Net11Union abstract-class polymorphism - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union abstract-class polymorphism - ERROR: {ex.Message}");
            }
        }
    }
}
#endif

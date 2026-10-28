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
                var back = LuminPackSerializer.Deserialize<Net11Pet>(LuminPackSerializer.Serialize(p));
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
                var json = LuminPackSerializer.SerializeJson(p);
                var back = LuminPackSerializer.DeserializeJson<Net11Pet>(json);
                results.Add(back.Value is UnionDog d && d.Name == "doggie"
                    ? "✓ Net11Union C# union JSON - PASSED"
                    : "✗ Net11Union C# union JSON - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union C# union JSON - ERROR: {ex.Message}");
            }

            // Sizeof agrees with serialized length
            try
            {
                var p = new Net11Pet(new UnionCat { Id = 3 });
                int size = LuminPackSerializer.Sizeof(p);
                int actual = LuminPackSerializer.Serialize(p).Length;
                results.Add(size == actual
                    ? "✓ Net11Union Sizeof - PASSED"
                    : "✗ Net11Union Sizeof - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union Sizeof - ERROR: {ex.Message}");
            }

            // Generic union Option<T>
            try
            {
                var opt = new Net11Option<UnionCat>(new Some<UnionCat> { Data = new UnionCat { Id = 5 } });
                var back = LuminPackSerializer.Deserialize<Net11Option<UnionCat>>(LuminPackSerializer.Serialize(opt));
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
                var back = LuminPackSerializer.Deserialize<Net11Maybe>(LuminPackSerializer.Serialize(m));
                results.Add(back.Value is int i && i == 42
                    ? "✓ Net11Union value-type case - PASSED"
                    : "✗ Net11Union value-type case - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ Net11Union value-type case - ERROR: {ex.Message}");
            }

            // Abstract-class polymorphism (existing mechanism) still works on .NET 11
            try
            {
                Net11Animal animal = new Net11Dog { Legs = 4, Bark = "woof" };
                var back = LuminPackSerializer.Deserialize<Net11Animal>(LuminPackSerializer.Serialize(animal));
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

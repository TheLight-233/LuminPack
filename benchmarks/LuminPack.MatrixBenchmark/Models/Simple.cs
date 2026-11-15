
using System.Buffers;
using LuminPack;
using LuminPack.Utility;

namespace LuminPackBenchmark;

using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Attribute;
using MemoryPack;
using Nino.Core;


#nullable disable

/// <summary>Provides simple class base functionality.</summary>
[LuminPackable]

[MemoryPackable]
[MemoryPackUnion(1, typeof(SimpleClass))]


[System.Text.Json.Serialization.JsonDerivedType(typeof(SimpleClass), "SimpleClass")]
[NinoType]
public abstract partial class SimpleClassBase
{
    /// <summary>Stores the base id.</summary>
    public int BaseID;

    /// <summary>Writes  to the current output.</summary>
    public abstract void Write();
}


/// <summary>Provides simple class functionality.</summary>
[MemoryPackable]

[LuminPackable]

[NinoType]
public sealed partial class SimpleClass : SimpleClassBase, Ifoo
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the tag.</summary>
    public bool Tag;
    /// <summary>Stores the numbers.</summary>
    public int[] Numbers;
    /// <summary>Stores the numbers2.</summary>
    public List<Transform> Numbers2;
    /// <summary>Stores the map1.</summary>
    public Dictionary<int, IFoo> Map1;
    /// <summary>Stores the foo.</summary>
    public FooD Foo;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static SimpleClass Create()
    {
        Random random = new Random(1729);
        return new SimpleClass
        {
            Id = random.Next(),
            Tag = random.Next() % 2 == 0,
            Numbers = Enumerable.Range(0, 100).Select(_ => random.Next()).ToArray(),
            Numbers2 = Enumerable.Range(0, 100).Select(x => new Transform(x, x, x)).ToList(),
            Map1 = new Dictionary<int, IFoo>()
            {
                {1, FooA.Create()},
                {2, FooB.Create()},
                {3, FooC.Create()},
                {4, FooD.Create()},
                {5, FooA.Create()},
                {6, FooB.Create()},
                {7, FooC.Create()},
                {8, FooD.Create()},
                {9, FooA.Create()},
                {10, FooB.Create()},
                {11, FooC.Create()},
                {12, FooD.Create()},
            },
            Foo = new FooD()
            {
                Card = new Transform(random.Next(), random.Next(), random.Next()),
                CardB = new Transform(random.Next(), random.Next(), random.Next()),
                CardC = new Transform(random.Next(), random.Next(), random.Next()),
                CardD = new Transform(random.Next(), random.Next(), random.Next()),
            },
        };
    }

    /// <summary>Stores the instance.</summary>
    public static SimpleClass Instance = new();

    //[LuminPackPoolRent]
    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    public static SimpleClass Rent()
    {
        return Instance;
    }

    //[LuminPackOnSerializing]
    /// <summary>Measures on serializing for the configured benchmark input.</summary>
    public static void OnSerializing()
    {
        Console.WriteLine("OnSerializing");
    }

    //[LuminPackOnSerialized]
    /// <summary>Measures on serialized for the configured benchmark input.</summary>
    public void OnSerialized()
    {
        Console.WriteLine("OnSerialized");
    }

    //[LuminPackOnDeserialized]
    /// <summary>Measures on deserialized for the configured benchmark input.</summary>
    public void OnDeserialized()
    {
        Console.WriteLine("OnDeserialized");
    }

    //[LuminPackOnDeserializing]
    /// <summary>Measures on deserializing for the configured benchmark input.</summary>
    public static void OnDeserializing()
    {
        Console.WriteLine("OnDeserializing");
    }



    /// <summary>Writes  to the current output.</summary>
    public override void Write() => Console.WriteLine("Yeah！！！");
}


/// <summary>Provides transform functionality.</summary>
[MemoryPackable]

[LuminPackable]

[LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
[NinoType]
public partial struct Transform
{
    /// <summary>Stores the x.</summary>
    public float x;
    /// <summary>Stores the y.</summary>
    public float y;
    /// <summary>Stores the z.</summary>
    public float z;

    /// <summary>Initializes a new instance of <c>Transform</c>.</summary>
    public Transform()
    {
    }

    /// <summary>Initializes a new instance of <c>Transform</c>.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <param name="y">The y used by this operation.</param>
    /// <param name="z">The z used by this operation.</param>
    [LuminPackConstructor]
    [NinoConstructor]
    //[MemoryPackConstructor]

    public Transform(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

}

/// <summary>Provides simple benchmark functionality.</summary>

using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable 8618


/// <summary>Provides a functionality.</summary>
[LuminPackable]
public class A
{
    /// <summary>Gets or sets the val.</summary>
    public int Val { get; set; }
}

/// <summary>Provides b functionality.</summary>
[LuminPackable]
public struct B
{
    /// <summary>Gets or sets the val.</summary>
    public int Val { get; set; }
}

/// <summary>Provides c functionality.</summary>
[LuminPackable]
public class C
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the as.</summary>
    public List<A> As { get; set; }

    /// <summary>Verifies to string.</summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString()
    {
        return $"{Name}={string.Join(",", As.Select(a => a.Val))}";
    }
}

/// <summary>Provides d functionality.</summary>
[LuminPackable]
public class D
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the bs.</summary>
    public List<B> Bs { get; set; }

    /// <summary>Verifies to string.</summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString()
    {
        return $"{Name}={string.Join(",", Bs.Select(b => b.Val))}";
    }
}

/// <summary>Provides e functionality.</summary>
[LuminPackable]
public class E
{
    /// <summary>Gets or sets the child.</summary>
    public E Child { get; set; }
}

/// <summary>Provides code gen serialization test functionality.</summary>
[LuminPackable]
public class CodeGenSerializationTest
{
    /// <summary>Verifies test code gen.</summary>
    public void TestCodeGen()
    {
        C c = new C()
        {
            Name = "test",
            As = new List<A>()
            {
                new A() { Val = 1 },
                new A() { Val = 2 },
                new A() { Val = 3 }
            }
        };
        D d = new D()
        {
            Name = "test",
            Bs = new List<B>()
            {
                new B() { Val = 1 },
                new B() { Val = 2 },
                new B() { Val = 3 }
            }
        };

        var bufC = LuminPackSerializer.Serialize(c);
        var bufD = LuminPackSerializer.Serialize(d);

        Console.WriteLine(string.Join(", ", bufC));
        Console.WriteLine(string.Join(", ", bufD));

        C c2 = LuminPackSerializer.Deserialize<C>(bufC);
        D d2 = LuminPackSerializer.Deserialize<D>(bufD);


        Console.WriteLine(c2.ToString());
    }
}

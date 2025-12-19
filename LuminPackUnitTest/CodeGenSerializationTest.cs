using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

using System;
using System.Linq;
using System.Collections.Generic;

#pragma warning disable 8618


[LuminPackable]
public class A
{
    public int Val { get; set; }
}

[LuminPackable]
public struct B
{
    public int Val { get; set; }
}

[LuminPackable]
public class C
{
    public string Name { get; set; }
            
    public List<A> As { get; set; }

    public override string ToString()
    {
        return $"{Name}={string.Join(",", As.Select(a => a.Val))}";
    }
}
        
[LuminPackable]
public class D
{
    public string Name { get; set; }
            
    public List<B> Bs { get; set; }
            
    public override string ToString()
    {
        return $"{Name}={string.Join(",", Bs.Select(b => b.Val))}";
    }
}

[LuminPackable]
public class E
{
    public E Child { get; set; }
}
    
[LuminPackable]
public class CodeGenSerializationTest
{
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
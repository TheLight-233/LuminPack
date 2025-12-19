using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Running;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Code.Core;
using LuminPack.Generated;
using LuminPack.Internal;
using LuminPack.Parsers;
using LuminPackBenchmark;
using Nino.Core;

class MyClass
{
    static void Main()
    { 
        BenchmarkRunner.Run<SimpleBenchmark>(); 
        
        var json = JsonTest.Create();
        
        var str = LuminPackSerializer.SerializeJson(json);
        
        Console.WriteLine(str);

        var tr = LuminPackSerializer.DeserializeJson<JsonTest>(str);
        Console.WriteLine(tr.value1);
        Console.WriteLine(tr.value2);
        Console.WriteLine(tr.value4[6]);
        Console.WriteLine(tr.value5[6]);
        Console.WriteLine(tr.value6.Contains("6"));
        Console.WriteLine(tr.type);
        Console.WriteLine(tr.value7.Item2);
    }
}


[LuminPackable]
public class JsonTest()
{
    public float value1;
    public string value2;
    public int[] value4;
    public Collection<string> value5;
    public ISet<string> value6;
    public Type type = typeof(JsonTest);
    public (int, string) value7 = (1, "123");
    

    public static JsonTest Create()
    {
        return new JsonTest()
        {
            value1 = 3.14f,
            value2 = "abc",
            value4 = Enumerable.Range(0, 10).ToArray(),
            value5 = new Collection<string>(Enumerable.Range(0, 10).Select(x => x.ToString()).ToList()),
            value6 = Enumerable.Range(0, 10).Select(x => x.ToString()).ToImmutableHashSet()
        };
    }
}


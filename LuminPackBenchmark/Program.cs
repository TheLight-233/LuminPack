using BenchmarkDotNet.Running;
using LuminPack;
using LuminPack.Code;
using LuminPackBenchmark;
using Nino.Core;

class MyClass
{
    static void Main()
    {
        BenchmarkRunner.Run<SimpleBenchmark>();
        SimpleClassBase myClass = SimpleClass.Create();
        var buffer = LuminPackSerializer.Serialize(myClass);
        var buffer2 = NinoSerializer.Serialize(myClass);
        var res = LuminPackSerializer.Deserialize<SimpleClassBase>(buffer);
        var obj = LuminPackMarshal.As<SimpleClassBase, SimpleClass>(ref res);
        Console.WriteLine(obj.Map1[1].Id);
    }
}

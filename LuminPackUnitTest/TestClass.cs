using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using LuminPack.Attribute;

#nullable disable

namespace LuminPackUnitTest;

[LuminPackable]
public partial class HierarchicalBase
{
    [LuminPackOrder(0)] protected int A;
    [LuminPackOrder(1)] public string B;
}

[LuminPackable]
public partial class HierarchicalSub1 : HierarchicalBase
{
    [LuminPackOrder(0)] protected bool C;
    [LuminPackOrder(1)] public float D;
}

[LuminPackable]
public partial class HierarchicalSub2 : HierarchicalSub1
{
    [LuminPackOrder(0)] protected bool E;
    [LuminPackOrder(1)] public List<int> F;
}

[LuminPackable]
public class SomeNestedPrivateEnum
{
    public int Id;

    [LuminPackIgnore] private PrivateEnum _someEnum = PrivateEnum.B;

    [LuminPackIgnore]
    public int EnumVal
    {
        get => (int)_someEnum;
        set => _someEnum = (PrivateEnum)value;
    }

    
}

[Flags]
public enum PrivateEnum
{
    A = 1,
    B = 2
}

[LuminPackable]
public class Move
{
    public ushort ClientX;

    public ushort ClientY;

    public Move()
    {
    }
    
    public static Move Create(ushort clientX, ushort clientY)
    {
        return new Move
        {
            ClientX = clientX,
            ClientY = clientY,
        };
    }
}

[LuminPackable]
public partial class TestMethodCtor
{
    [LuminPackOrder(0)] public int A;
    [LuminPackOrder(1)] private string _b;

    public string B
    {
        get => _b;
        set => _b = value ?? string.Empty;
    }
    
    public static TestMethodCtor Create(int a = 0, string b = null)
    {
        Console.WriteLine(111);
        return new TestMethodCtor()
        {
            A = a,
            _b = b ?? string.Empty
        };
    }
}

[LuminPackable]
public partial class TestA<T>
{
    [LuminPackOrder(0)] 
    [LuminPackInclude]
    protected T Value;

    public T MValue => Value;
    
    public TestA(T value = default) => Value = value;

    public static implicit operator T(TestA<T> val) => val.Value;
}

[LuminPackable]
public partial class TestB<T> : TestA<T>
{
    
    public TestB(T value = default) : base(value)
    {
    }
}

[LuminPackable]
public class CursedGeneric<T>
{
    public ConcurrentDictionary<string, T[]> field;
}

[LuminPackable]
public class PrivateNestedCollection
{
    private struct MyStruct
    {
        public int X;
        public string Y;
    }

    private class MyClass
    {
        public int X;
        public string Y;
    }

    public void Func()
    {
        //ensure no code is generated for this
        List<MyStruct> list = new List<MyStruct>();
        Dictionary<MyStruct, MyStruct> dict = new Dictionary<MyStruct, MyStruct>();
        List<MyClass> list2 = new List<MyClass>();
        Dictionary<MyClass, MyClass> dict2 = new Dictionary<MyClass, MyClass>();
        Dictionary<MyStruct, MyClass> dict3 = new Dictionary<MyStruct, MyClass>();
    }
}

[LuminPackable]
public interface IListElementClass
{
}

[LuminPackable]
public class ListElementClass : IListElementClass
{
    public int Id;
    public string Name;
    public DateTime CreateTime;
    public bool Extra;
}

[LuminPackable]
public class ListElementClass2Renamed : IListElementClass
{
    public int Id;
    public string Name;
    public DateTime CreateTime;
    public string Extra;
}


[LuminPackable]
public
#if !NET8_0_OR_GREATER
    partial
#endif
    class ProtectedShouldInclude
{
    [LuminPackInclude]
    protected int _id;

    [LuminPackIgnore]
    public int Id
    {
        get => _id;
        set => _id = value;
    }
}

[LuminPackable]
public
#if !NET8_0_OR_GREATER
    partial
#endif
    class ShouldIgnorePrivate
{
    private int _id;

    public int ID => _id;

    [LuminPackIgnore]
    public int Id
    {
        get => _id;
        set => _id = value;
    }

    public string Name;
    public DateTime CreateTime;
}

//We make it no namespace on purpose to see if it still works
[LuminPackable]
public
#if !NET8_0_OR_GREATER
    partial
#endif
    class Bindable<T>
{
    [LuminPackOrder(0)]
    [LuminPackInclude]
    private T mValue;
    
    public T item { get; }

    public T Value
    {
        get => mValue;
        set => mValue = value;
    }
    
    public Bindable(T mValue)
    {
        this.mValue = mValue;
    }
}


[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    class
    TestPrivateMemberClass : Base
{
    private int Id { get; set; }

    public TestPrivateMemberClass()
    {
        Id = Random.Shared.Next();
    }
}

[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    record RecordWithPrivateMember(string Name)
{
    private int Id { get; set; }
}

[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    record struct RecordWithPrivateMember2(string Name)
{
    private int Id { get; set; } = 0;
}

[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    struct StructWithPrivateMember
{
    public int Id;
    [LuminPackInclude]
    private string Name;

    public string GetName() => Name;
    public void SetName(string name) => Name = name;
}

[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    class ClassWithPrivateMember<T>
{
    public int Id;
    [LuminPackIgnore] public bool Flag;

    private string _name;
    private List<T> _list;

    private bool _flagProperty
    {
        get => Flag;
        set => Flag = value;
    }

    [LuminPackIgnore]
    public List<T> List
    {
        get => _list;
        set => _list = value;
    }

    public ClassWithPrivateMember()
    {
        Id = 0;
        _name = Guid.NewGuid().ToString();
    }
}

[LuminPackable]
public interface ISerializable
{
}

[LuminPackable]
public struct Struct1 : ISerializable
{
    public int A;
    public DateTime B;
    public Guid C;
}

[LuminPackable]
public class Class1 : ISerializable
{
    public int A;
    public DateTime B;
    public Guid C;
    public ISerializable D;
}

[LuminPackable]
public struct Struct2 : ISerializable
{
    public int A;
    public DateTime B;
    public string C;
    public Class1 D;
}

[LuminPackable]
public class StringData
{
    public string Str;
    public bool ShouldHaveNoEffect;
}

[LuminPackable]
public class StringData2
{
    public string Str;
    public bool ShouldHaveNoEffect;
}

[LuminPackable]
public class SaveData
{
    [LuminPackOrder(1)] public int Id;
    [LuminPackOrder(2)] public string Name;
    [LuminPackOrder(3)] public DateTime NewField1;
    [LuminPackOrder(4)] public Generic<int> NewField2;
}

[LuminPackable]
public struct GenericStruct<T>
{
    public T Val;
}

[LuminPackable]
public class Generic<T>
{
    public T Val;
}

[LuminPackable]
public class ComplexGeneric<T> where T : IList
{
    public T Val;
}

[LuminPackable]
public class ComplexGeneric2<T>
{
    public Generic<T> Val;
}

[LuminPackable]
public abstract class Base
{
    public int A;
}

[LuminPackable]
public class Sub1 : Base
{
    public int B;
}

public class Nested
{
    [LuminPackable]
    public abstract class Sub2 : Base
    {
        public int C;
    }

    [LuminPackable]
    public class Sub2Impl : Sub2
    {
        public Sub4 D;
    }

    public class Sub4
    {
        public int D;
    }
}

[LuminPackable]
public class Sub3 : Nested.Sub2Impl
{
    public int E;
}

[LuminPackable]
public class TestClass
{
    [LuminPackOrder(1)] public int A;

    [LuminPackOrder(2)] public string B;
}

[LuminPackable]
public class TestClass2 : TestClass
{
    [LuminPackOrder(3)] public int C;
}

[LuminPackable]
public class TestClass3 : TestClass2
{
    public bool D;
    public TestClass E;
    public TestStruct F;
    public TestStruct? G;
    public IList<TestStruct2> H;
    public List<TestStruct2?> I;
    public TestStruct3[] J;
    public Dictionary<int, int> K;
    public Dictionary<int, TestClass3> L;
    public TestClass3 M;
}

[LuminPackable]
public struct TestStruct
{
    public int A;
    public string B;
}

[LuminPackable]
public struct TestStruct2
{
    public int A;
    public bool B;
    public TestStruct3 C;
}

public struct TestStruct3
{
    public byte A;
    public float B;
}

[LuminPackable]
public class SimpleClass
{
    public int Id;
    public string Name;
    public DateTime CreateTime;
}

[LuminPackable]
public record struct SimpleRecordStruct(int Id, string Name, DateTime CreateTime);

[LuminPackable]
public record struct SimpleRecordStruct2(int Id, DateTime CreateTime);

[LuminPackable]
public record struct SimpleRecordStruct2<T>(int Id, T Data);

[LuminPackable]
public record SimpleRecord
{
    public int Id;
    public string Name;
    public DateTime CreateTime;

    public SimpleRecord()
    {
        Id = 0;
        Name = string.Empty;
        CreateTime = DateTime.MinValue;
    }

    [LuminPackConstructor]
    public SimpleRecord(int id, string name)
    {
        Id = id;
        Name = name;
        CreateTime = DateTime.Now;
    }
}

[LuminPackable]
public record SimpleRecord2(int Id, string Name, DateTime CreateTime);

[LuminPackable]
public record SimpleRecord3(
    [property: LuminPackOrder(3)] int Id,
    [property: LuminPackOrder(2)] string Name,
    [property: LuminPackOrder(1)] DateTime CreateTime)
{
    [LuminPackOrder(4)] public bool Flag;

    public int Ignored;
}


[LuminPackable]
public record SimpleRecord4(int Id, string Name, DateTime CreateTime)
{
    [LuminPackIgnore] public bool Flag;

    public int ShouldNotIgnore;

    // Should not use this
    [LuminPackConstructor]
    public SimpleRecord4() : this(0, "", DateTime.MinValue)
    {
    }
}


[LuminPackable]
public record SimpleRecord5(int Id, string Name, DateTime CreateTime)
{
    [LuminPackIgnore] public bool Flag;

    public int ShouldNotIgnore;

    // Not good since we will discard the primary constructor values when deserializing
    [LuminPackConstructor]
    public SimpleRecord5() : this(0, "", DateTime.MinValue)
    {
    }
}

[LuminPackable]
public record SimpleRecord6<T>(int Id, T Data);


[LuminPackable]
public struct SimpleStruct
{
    public int Id;
    public string Name;
    public DateTime CreateTime;

    [LuminPackConstructor]
    public SimpleStruct(int id, string name, DateTime createTime)
    {
        Id = id;
        Name = name;
        CreateTime = createTime;
    }
}

[LuminPackable]
public class SimpleClassWithConstructor
{
    public int Id;
    public string Name;
    public DateTime CreateTime;

    // [NinoConstructor(nameof(Id), nameof(Name), nameof(CreateTime))] - we try not to use this and test if it still works
    // should automatically use this constructor since this is the only public constructor
    public SimpleClassWithConstructor(int id, string name, DateTime createTime)
    {
        Id = id;
        Name = name;
        CreateTime = createTime;
    }
}
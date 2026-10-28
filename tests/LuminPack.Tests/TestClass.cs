using System.Collections;
using System.Collections.Concurrent;
using LuminPack.Attribute;

#nullable disable

namespace LuminPackUnitTest;

/// <summary>Provides hierarchical base functionality.</summary>
[LuminPackable]
public partial class HierarchicalBase
{
    /// <summary>Stores the a.</summary>
    [LuminPackOrder(0)] protected int A;
    /// <summary>Stores the b.</summary>
    [LuminPackOrder(1)] public string B;
}

/// <summary>Provides hierarchical sub1 functionality.</summary>
[LuminPackable]
public partial class HierarchicalSub1 : HierarchicalBase
{
    /// <summary>Stores the c.</summary>
    [LuminPackOrder(0)] protected bool C;
    /// <summary>Stores the d.</summary>
    [LuminPackOrder(1)] public float D;
}

/// <summary>Provides hierarchical sub2 functionality.</summary>
[LuminPackable]
public partial class HierarchicalSub2 : HierarchicalSub1
{
    /// <summary>Stores the e.</summary>
    [LuminPackOrder(0)] protected bool E;
    /// <summary>Stores the f.</summary>
    [LuminPackOrder(1)] public List<int> F;
}

/// <summary>Provides some nested private enum functionality.</summary>
[LuminPackable]
public class SomeNestedPrivateEnum
{
    /// <summary>Stores the id.</summary>
    public int Id;

    [LuminPackIgnore] private PrivateEnum _someEnum = PrivateEnum.B;

    /// <summary>Gets or sets the enum val.</summary>
    [LuminPackIgnore]
    public int EnumVal
    {
        get => (int)_someEnum;
        set => _someEnum = (PrivateEnum)value;
    }


}

/// <summary>Provides private enum functionality.</summary>
[Flags]
public enum PrivateEnum
{
    /// <summary>Specifies a.</summary>
    A = 1,
    /// <summary>Specifies b.</summary>
    B = 2
}

/// <summary>Provides move functionality.</summary>
[LuminPackable]
public class Move
{
    /// <summary>Stores the client x.</summary>
    public ushort ClientX;

    /// <summary>Stores the client y.</summary>
    public ushort ClientY;

    /// <summary>Initializes a new instance of <c>Move</c>.</summary>
    public Move()
    {
    }

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="clientX">The client x used by this operation.</param>
    /// <param name="clientY">The client y used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static Move Create(ushort clientX, ushort clientY)
    {
        return new Move
        {
            ClientX = clientX,
            ClientY = clientY,
        };
    }
}

/// <summary>Provides test method ctor functionality.</summary>
[LuminPackable]
public partial class TestMethodCtor
{
    /// <summary>Stores the a.</summary>
    [LuminPackOrder(0)] public int A;
    [LuminPackOrder(1)] private string _b;

    /// <summary>Gets or sets the b.</summary>
    public string B
    {
        get => _b;
        set => _b = value ?? string.Empty;
    }

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <param name="a">The a used by this operation.</param>
    /// <param name="b">The b used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

/// <summary>Provides test a functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public partial class TestA<T>
{
    /// <summary>Stores the value.</summary>
    [LuminPackOrder(0)]
    [LuminPackInclude]
    protected T Value;

    /// <summary>Gets the m value.</summary>
    public T MValue => Value;

    /// <summary>Initializes a new instance of <c>TestA</c>.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    public TestA(T value = default) => Value = value;

    /// <summary>Converts the supplied value to <c>T</c>.</summary>
    /// <param name="val">The val used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static implicit operator T(TestA<T> val) => val.Value;
}

/// <summary>Provides test b functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public partial class TestB<T> : TestA<T>
{

    /// <summary>Initializes a new instance of <c>TestB</c>.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    public TestB(T value = default) : base(value)
    {
    }
}

/// <summary>Provides cursed generic functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public class CursedGeneric<T>
{
    /// <summary>Stores the field.</summary>
    public ConcurrentDictionary<string, T[]> field;
}

/// <summary>Provides private nested collection functionality.</summary>
[LuminPackable]
public class PrivateNestedCollection
{
    /// <summary>Provides my struct functionality.</summary>
    private struct MyStruct
    {
        /// <summary>Stores the x.</summary>
        public int X;
        /// <summary>Stores the y.</summary>
        public string Y;
    }

    /// <summary>Provides my class functionality.</summary>
    private class MyClass
    {
        /// <summary>Stores the x.</summary>
        public int X;
        /// <summary>Stores the y.</summary>
        public string Y;
    }

    /// <summary>Verifies func.</summary>
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

/// <summary>Provides i list element class functionality.</summary>
[LuminPackable]
public partial interface IListElementClass
{
}

/// <summary>Provides list element class functionality.</summary>
[LuminPackable]
public partial class ListElementClass : IListElementClass
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;
    /// <summary>Stores the extra.</summary>
    public bool Extra;
}

/// <summary>Provides list element class2 renamed functionality.</summary>
[LuminPackable]
public partial class ListElementClass2Renamed : IListElementClass
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;
    /// <summary>Stores the extra.</summary>
    public string Extra;
}


/// <summary>Provides protected should include functionality.</summary>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
    partial
#endif
    class ProtectedShouldInclude
{
    /// <summary>Stores the id.</summary>
    [LuminPackInclude]
    protected int _id;

    /// <summary>Gets or sets the id.</summary>
    [LuminPackIgnore]
    public int Id
    {
        get => _id;
        set => _id = value;
    }
}

/// <summary>Provides should ignore private functionality.</summary>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
    partial
#endif
    class ShouldIgnorePrivate
{
    private int _id;

    /// <summary>Gets the id.</summary>
    public int ID => _id;

    /// <summary>Gets or sets the id.</summary>
    [LuminPackIgnore]
    public int Id
    {
        get => _id;
        set => _id = value;
    }

    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;
}

//We make it no namespace on purpose to see if it still works
/// <summary>Provides bindable functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
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

    /// <summary>Gets the item.</summary>
    public T item { get; }

    /// <summary>Gets or sets the value.</summary>
    public T Value
    {
        get => mValue;
        set => mValue = value;
    }

    /// <summary>Initializes a new instance of <c>Bindable</c>.</summary>
    /// <param name="mValue">The m value used by this operation.</param>
    public Bindable(T mValue)
    {
        this.mValue = mValue;
    }
}


/// <summary>Provides test private member class functionality.</summary>
[LuminPackable]
public
    partial
    class
    TestPrivateMemberClass : Base
{
    /// <summary>Gets or sets the id.</summary>
    private int Id { get; set; }

    /// <summary>Initializes a new instance of <c>TestPrivateMemberClass</c>.</summary>
    public TestPrivateMemberClass()
    {
        Id = Random.Shared.Next();
    }
}

/// <summary>Provides record with private member functionality.</summary>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    record RecordWithPrivateMember(string Name)
{
    /// <summary>Gets or sets the id.</summary>
    private int Id { get; set; }
}

/// <summary>Provides record with private member2 functionality.</summary>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    record struct RecordWithPrivateMember2(string Name)
{
    /// <summary>Gets or sets the id.</summary>
    private int Id { get; set; } = 0;
}

/// <summary>Provides struct with private member functionality.</summary>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    struct StructWithPrivateMember
{
    /// <summary>Stores the id.</summary>
    public int Id;
    [LuminPackInclude]
    private string Name;

    /// <summary>Obtains the name for the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    public string GetName() => Name;
    /// <summary>Updates the name used by the current operation.</summary>
    /// <param name="name">The name used by this operation.</param>
    public void SetName(string name) => Name = name;
}

/// <summary>Provides class with private member functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public
#if !NET8_0_OR_GREATER
        partial
#endif
    class ClassWithPrivateMember<T>
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the flag.</summary>
    [LuminPackIgnore] public bool Flag;

    private string _name;
    private List<T> _list;

    /// <summary>Gets or sets the flag property.</summary>
    private bool _flagProperty
    {
        get => Flag;
        set => Flag = value;
    }

    /// <summary>Gets or sets the list.</summary>
    [LuminPackIgnore]
    public List<T> List
    {
        get => _list;
        set => _list = value;
    }

    /// <summary>Initializes a new instance of <c>ClassWithPrivateMember</c>.</summary>
    public ClassWithPrivateMember()
    {
        Id = 0;
        _name = Guid.NewGuid().ToString();
    }
}

/// <summary>Provides i serializable functionality.</summary>
[LuminPackable]
public partial interface ISerializable
{
}

/// <summary>Provides struct1 functionality.</summary>
[LuminPackable]
public partial struct Struct1 : ISerializable
{
    /// <summary>Stores the a.</summary>
    public int A;
    /// <summary>Stores the b.</summary>
    public DateTime B;
    /// <summary>Stores the c.</summary>
    public Guid C;
}

/// <summary>Provides class1 functionality.</summary>
[LuminPackable]
public partial class Class1 : ISerializable
{
    /// <summary>Stores the a.</summary>
    public int A;
    /// <summary>Stores the b.</summary>
    public DateTime B;
    /// <summary>Stores the c.</summary>
    public Guid C;
    /// <summary>Stores the d.</summary>
    public ISerializable D;
}

/// <summary>Provides struct2 functionality.</summary>
[LuminPackable]
public partial struct Struct2 : ISerializable
{
    /// <summary>Stores the a.</summary>
    public int A;
    /// <summary>Stores the b.</summary>
    public DateTime B;
    /// <summary>Stores the c.</summary>
    public string C;
    /// <summary>Stores the d.</summary>
    public Class1 D;
}

/// <summary>Provides string data functionality.</summary>
[LuminPackable]
public class StringData
{
    /// <summary>Stores the str.</summary>
    public string Str;
    /// <summary>Stores the should have no effect.</summary>
    public bool ShouldHaveNoEffect;
}

/// <summary>Provides string data2 functionality.</summary>
[LuminPackable]
public class StringData2
{
    /// <summary>Stores the str.</summary>
    public string Str;
    /// <summary>Stores the should have no effect.</summary>
    public bool ShouldHaveNoEffect;
}

/// <summary>Provides save data functionality.</summary>
[LuminPackable]
public class SaveData
{
    /// <summary>Stores the id.</summary>
    [LuminPackOrder(1)] public int Id;
    /// <summary>Stores the name.</summary>
    [LuminPackOrder(2)] public string Name;
    /// <summary>Stores the new field1.</summary>
    [LuminPackOrder(3)] public DateTime NewField1;
    /// <summary>Stores the new field2.</summary>
    [LuminPackOrder(4)] public Generic<int> NewField2;
}

/// <summary>Provides generic struct functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public struct GenericStruct<T>
{
    /// <summary>Stores the val.</summary>
    public T Val;
}

/// <summary>Provides generic functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public class Generic<T>
{
    /// <summary>Stores the val.</summary>
    public T Val;
}

/// <summary>Provides complex generic functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public class ComplexGeneric<T> where T : IList
{
    /// <summary>Stores the val.</summary>
    public T Val;
}

/// <summary>Provides complex generic2 functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public class ComplexGeneric2<T>
{
    /// <summary>Stores the val.</summary>
    public Generic<T> Val;
}

/// <summary>Provides base functionality.</summary>
[LuminPackable]
public abstract partial class Base
{
    /// <summary>Stores the a.</summary>
    public int A;
}

/// <summary>Provides sub1 functionality.</summary>
[LuminPackable]
public partial class Sub1 : Base
{
    /// <summary>Stores the b.</summary>
    public int B;
}

/// <summary>Provides nested functionality.</summary>
public partial class Nested
{
    /// <summary>Provides sub2 functionality.</summary>
    [LuminPackable]
    public abstract partial class Sub2 : Base
    {
        /// <summary>Stores the c.</summary>
        public int C;
    }

    /// <summary>Provides sub2 impl functionality.</summary>
    [LuminPackable]
    public partial class Sub2Impl : Sub2
    {
        /// <summary>Stores the d.</summary>
        public Sub4 D;
    }

    /// <summary>Provides sub4 functionality.</summary>
    public class Sub4
    {
        /// <summary>Stores the d.</summary>
        public int D;
    }
}

/// <summary>Provides sub3 functionality.</summary>
[LuminPackable]
public partial class Sub3 : Nested.Sub2Impl
{
    /// <summary>Stores the e.</summary>
    public int E;
}

/// <summary>Provides test class functionality.</summary>
[LuminPackable]
public class TestClass
{
    /// <summary>Stores the a.</summary>
    [LuminPackOrder(1)] public int A;

    /// <summary>Stores the b.</summary>
    [LuminPackOrder(2)] public string B;
}

/// <summary>Provides test class2 functionality.</summary>
[LuminPackable]
public class TestClass2 : TestClass
{
    /// <summary>Stores the c.</summary>
    [LuminPackOrder(3)] public int C;
}

/// <summary>Provides test class3 functionality.</summary>
[LuminPackable]
public class TestClass3 : TestClass2
{
    /// <summary>Stores the d.</summary>
    [LuminPackOrder(4)]
    public bool D;
    /// <summary>Stores the e.</summary>
    [LuminPackOrder(5)]
    public TestClass E;
    /// <summary>Stores the f.</summary>
    [LuminPackOrder(6)]
    public TestStruct F;
    /// <summary>Stores the g.</summary>
    [LuminPackOrder(7)]
    public TestStruct? G;
    /// <summary>Stores the h.</summary>
    [LuminPackOrder(8)]
    public IList<TestStruct2> H;
    /// <summary>Stores the i.</summary>
    [LuminPackOrder(9)]
    public List<TestStruct2?> I;
    /// <summary>Stores the j.</summary>
    [LuminPackOrder(10)]
    public TestStruct3[] J;
    /// <summary>Stores the k.</summary>
    [LuminPackOrder(11)]
    public Dictionary<int, int> K;
    /// <summary>Stores the l.</summary>
    [LuminPackOrder(12)]
    public Dictionary<int, TestClass3> L;
    /// <summary>Stores the m.</summary>
    [LuminPackOrder(13)]
    public Dictionary<int, TestClass3> M;
}

/// <summary>Provides test struct functionality.</summary>
[LuminPackable]
public struct TestStruct
{
    /// <summary>Stores the a.</summary>
    public int A;
    /// <summary>Stores the b.</summary>
    public string B;
}

/// <summary>Provides test struct2 functionality.</summary>
[LuminPackable]
public struct TestStruct2
{
    /// <summary>Stores the a.</summary>
    public int A;
    /// <summary>Stores the b.</summary>
    public bool B;
    /// <summary>Stores the c.</summary>
    public TestStruct3 C;
}

/// <summary>Provides test struct3 functionality.</summary>
[LuminPackable]
public struct TestStruct3
{
    /// <summary>Stores the a.</summary>
    public byte A;
    /// <summary>Stores the b.</summary>
    public float B;
}

/// <summary>Provides simple class functionality.</summary>
[LuminPackable]
public class SimpleClass
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;
}

/// <summary>Provides simple record struct functionality.</summary>
[LuminPackable]
public record struct SimpleRecordStruct(int Id, string Name, DateTime CreateTime);

/// <summary>Provides simple record struct2 functionality.</summary>
[LuminPackable]
public record struct SimpleRecordStruct2(int Id, DateTime CreateTime);

/// <summary>Provides simple record struct2 functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public record struct SimpleRecordStruct2<T>(int Id, T Data);

/// <summary>Provides simple record functionality.</summary>
[LuminPackable]
public record SimpleRecord
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;

    /// <summary>Initializes a new instance of <c>SimpleRecord</c>.</summary>
    public SimpleRecord()
    {
        Id = 0;
        Name = string.Empty;
        CreateTime = DateTime.MinValue;
    }

    /// <summary>Initializes a new instance of <c>SimpleRecord</c>.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    [LuminPackConstructor]
    public SimpleRecord(int id, string name)
    {
        Id = id;
        Name = name;
        CreateTime = DateTime.Now;
    }
}

/// <summary>Provides simple record2 functionality.</summary>
[LuminPackable]
public record SimpleRecord2(int Id, string Name, DateTime CreateTime);

/// <summary>Provides simple record3 functionality.</summary>
[LuminPackable]
public record SimpleRecord3(
    [property: LuminPackOrder(3)] int Id,
    [property: LuminPackOrder(2)] string Name,
    [property: LuminPackOrder(1)] DateTime CreateTime)
{
    /// <summary>Stores the flag.</summary>
    [LuminPackOrder(4)] public bool Flag;

    /// <summary>Stores the ignored.</summary>
    public int Ignored;
}


/// <summary>Provides simple record4 functionality.</summary>
[LuminPackable]
public record SimpleRecord4(int Id, string Name, DateTime CreateTime)
{
    /// <summary>Stores the flag.</summary>
    [LuminPackIgnore] public bool Flag;

    /// <summary>Stores the should not ignore.</summary>
    public int ShouldNotIgnore;

    // Should not use this
    /// <summary>Initializes a new instance of <c>SimpleRecord4</c>.</summary>
    [LuminPackConstructor]
    public SimpleRecord4() : this(0, "", DateTime.MinValue)
    {
    }
}


/// <summary>Provides simple record5 functionality.</summary>
[LuminPackable]
public record SimpleRecord5(int Id, string Name, DateTime CreateTime)
{
    /// <summary>Stores the flag.</summary>
    [LuminPackIgnore] public bool Flag;

    /// <summary>Stores the should not ignore.</summary>
    public int ShouldNotIgnore;

    // Not good since we will discard the primary constructor values when deserializing
    /// <summary>Initializes a new instance of <c>SimpleRecord5</c>.</summary>
    [LuminPackConstructor]
    public SimpleRecord5() : this(0, "", DateTime.MinValue)
    {
    }
}

/// <summary>Provides simple record6 functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[LuminPackable]
public record SimpleRecord6<T>(int Id, T Data);


/// <summary>Provides simple struct functionality.</summary>
[LuminPackable]
public struct SimpleStruct
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;

    /// <summary>Initializes a new instance of <c>SimpleStruct</c>.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="createTime">The create time used by this operation.</param>
    [LuminPackConstructor]
    public SimpleStruct(int id, string name, DateTime createTime)
    {
        Id = id;
        Name = name;
        CreateTime = createTime;
    }
}

/// <summary>Provides simple class with constructor functionality.</summary>
[LuminPackable]
public class SimpleClassWithConstructor
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the create time.</summary>
    public DateTime CreateTime;

    // [NinoConstructor(nameof(Id), nameof(Name), nameof(CreateTime))] - we try not to use this and test if it still works
    // should automatically use this constructor since this is the only public constructor
    /// <summary>Initializes a new instance of <c>SimpleClassWithConstructor</c>.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="createTime">The create time used by this operation.</param>
    public SimpleClassWithConstructor(int id, string name, DateTime createTime)
    {
        Id = id;
        Name = name;
        CreateTime = createTime;
    }
}


using LuminPack.Attribute;

[LuminPackable]
public class NoNameSpaceSerializationTest
{
    public int Id { get; set; }
    public string Name { get; set; }

    [LuminPackable]
    public class NoNameSpaceNestedClass<T>
    {
        public T Id { get; set; }
        public string Name { get; set; }
    }
}
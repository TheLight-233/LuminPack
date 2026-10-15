
using LuminPack.Attribute;

/// <summary>Provides no name space serialization test functionality.</summary>
[LuminPackable]
public class NoNameSpaceSerializationTest
{
    /// <summary>Gets or sets the id.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Provides no name space nested class functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    [LuminPackable]
    public class NoNameSpaceNestedClass<T>
    {
        /// <summary>Gets or sets the id.</summary>
        public T Id { get; set; }
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }
    }
}

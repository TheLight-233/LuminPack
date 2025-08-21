using System.Runtime.CompilerServices;

namespace LuminPack.Option
{
    public record LuminPackSerializerOption
    {
        //Default is utf8
        public static readonly LuminPackSerializerOption Default = 
            new LuminPackSerializerOption
            {
                StringEncoding =  LuminPackStringEncoding.UTF8,
                StringRecording = LuminPackStringRecording.Length,
            };
        
        public static readonly LuminPackSerializerOption Utf8 = Default with { StringEncoding = LuminPackStringEncoding.UTF8 };
        public static readonly LuminPackSerializerOption Utf16 =  Default with { StringEncoding = LuminPackStringEncoding.UTF16 };
        public static readonly LuminPackSerializerOption Token = Default with { StringRecording = LuminPackStringRecording.Token };
        public static readonly LuminPackSerializerOption Length = Default with { StringRecording = LuminPackStringRecording.Length };

        public static readonly LuminPackSerializerOption Utf16WithLength = new ()
        {
            StringEncoding =  LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Length,
        };
        
        
        public LuminPackStringEncoding StringEncoding { get; set; }
        public LuminPackStringRecording StringRecording { get; set; }
    }

    public enum LuminPackStringEncoding : byte
    {
        UTF8,
        UTF16,
    }

    public enum LuminPackStringRecording : byte
    {
        Token,
        Length,
    }
    
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        private ReferenceEqualityComparer() { }

        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
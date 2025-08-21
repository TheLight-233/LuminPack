using LuminPack.Attribute;

namespace LuminPack.Interface
{
    public interface IDataParser<T> : IData<T>
    {
        [Preserve]
        byte[] Serialize();
        
        [Preserve]
        T? Deserialize();
        
        [Preserve]
        T? Deserialize(T data);

        [Preserve]
        int Sizeof();
    }
}
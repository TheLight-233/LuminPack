using System.Threading.Tasks;
using LuminPack.Attribute;

namespace LuminPack.Interface
{
    public interface IDataParserAsync<T> : IDataAsync<T>
    {
        [Preserve]
        Task<byte[]> SerializeAsync();
        
        [Preserve]
        Task<T?> DeserializeAsync();
        
        [Preserve]
        Task<T?> DeserializeAsync(T data);
    }
}
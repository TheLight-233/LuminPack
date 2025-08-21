namespace LuminPack.Interface;

public interface IData<T>
{
    public IDataParser<T> LoadData(T? data);

    public IDataParser<T> LoadData(byte[]? data);
    
}

public interface IDataAsync<T>
{
    public IDataParserAsync<T> LoadDataAsync(T? data);

    public IDataParserAsync<T> LoadDataAsync(byte[]? data);
}
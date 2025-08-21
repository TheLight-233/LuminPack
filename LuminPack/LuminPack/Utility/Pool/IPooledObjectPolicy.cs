namespace LuminPack.Utility;

#if NET8_0_OR_GREATER
public interface IPooledObjectPolicy<T> 
    where T : class
{
    static abstract T Create();
    static abstract bool Return(T obj);
}
#else
public interface IPooledObjectPolicy<T>
    where T : class
{
    T Create();
    bool Return(T obj);
}
#endif

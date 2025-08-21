using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Interface;
using LuminPack.Data;
using LuminPack.Parsers;


namespace LuminPack
{
    public static partial class LuminPackParseProvider
    {

        // 同步工厂
        private static readonly ConcurrentDictionary<Type, Func<object>> _entitySyncFactor = new();

        // 异步工厂
        private static readonly ConcurrentDictionary<Type, Func<object>> _entityAsyncFactor = new();

        private static readonly ConcurrentDictionary<Type, Type> _genericCollectionParser = new();

        private static readonly ConcurrentDictionary<Type, Type> _interfaceParser = new();

        static partial void RegisterInitialParsers();

        static LuminPackParseProvider()
        {
            ParserFactory.Initialize();

            RegisterWellKnownTypesParsers();

            RegisterInitialParsers();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRegistered<T>() => Check<T>.Registered;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParserType GetParserType<T>() => Check<T>.ParserType;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register<T>(LuminData<T> luminData)
        {
            luminData.RegisterParser();

            Check<T>.Registered = true;
            Check<T>.ParserType = ParserType.Data;

            Cache<T>.parser = luminData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegisterParsers<T>(LuminPackParser<T> parser)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));

            Check<T>.Registered = true;
            Check<T>.ParserType = ParserType.Parsers;

            Cache<T>.parser = parser;
        }


        /// <summary>
        /// 注册同步解析器工厂
        /// </summary>
        public static void RegisterDataHandler<T>(Func<IDataParser<T>> factory)
        {
            _entitySyncFactor[typeof(T)] = factory;
        }

        /// <summary>
        /// 注册异步解析器工厂
        /// </summary>
        public static void RegisterAsyncHandler<T>(Func<IDataParserAsync<T>> factory)
        {
            _entityAsyncFactor[typeof(T)] = factory;
        }

        /// <summary>
        /// 获取解析器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDataParser<T>? GetDataParser<T>()
        {
            if (Cache<T>.parser is null)
                LuminPackExceptionHelper.ThrowNoParserRegistered(typeof(T));

            return Cache<T>.parser as IDataParser<T>;
        }

        /// <summary>
        /// 获取异步解析器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDataParserAsync<T>? GetDataParserAsync<T>()
        {
            if (Cache<T>.parser is null)
                LuminPackExceptionHelper.ThrowNoParserRegistered(typeof(T));

            return Cache<T>.parser as IDataParserAsync<T>;
        }

        /// <summary>
        /// 获取WellKnown解析器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ILuminPackableParser<T> GetParser<T>()
        {
            if (Cache<T>.parser is null)
                LuminPackExceptionHelper.ThrowNoParserRegistered(typeof(T));

            return Cache<T>.parser;
        }

        /// <summary>
        /// 获取Evaluator
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ILuminPackEvaluator<T> GetParserEvaluator<T>()
        {
            if (Cache<T>.parser is null)
                LuminPackExceptionHelper.ThrowNoParserRegistered(typeof(T));

            return Cache<T>.parser;
        }

        public static bool TryRegisterParser<T>()
        {
            if (Check<T>.Registered) return true;

            try
            {
                var type = typeof(T);

                if (TryRegisterConstructor<T>())
                {
                    return true;
                }
                    
                var typeIsReferenceOrContainsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                var f = CreateGenericParser(type, typeIsReferenceOrContainsReferences) as LuminPackParser<T>;

                return f is null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryRegisterConstructor<T>()
        {
            var parserType = ParserFactory.GetParser(typeof(T));
            
            if (parserType == null)
                return false;
            
            RuntimeHelpers.RunClassConstructor(parserType.TypeHandle);
            
            return true;
        }
        
        public static bool TryRegisterParser(Type type)
        {
            var parserType = ParserFactory.GetParser(type);
            
            if (parserType == null)
                return false;
            
            RuntimeHelpers.RunClassConstructor(parserType.TypeHandle);
            
            return true;
        }

        internal static object? CreateGenericParser(Type type, bool typeIsReferenceOrContainsReferences)
        {
            Type? parserType = null;

            if (type.IsArray)
            {
                if (type.IsSZArray)
                {
                    if (!typeIsReferenceOrContainsReferences)
                    {
                        parserType = typeof(DangerousUnmanagedArrayParser<>).MakeGenericType(type.GetElementType()!);
                        goto CREATE;
                    }
                    else
                    {
                        parserType = typeof(ArrayParser<>).MakeGenericType(type.GetElementType()!);
                        goto CREATE;
                    }
                }
                else
                {
                    var rank = type.GetArrayRank();
                    switch (rank)
                    {
                        case 2:
                            parserType = typeof(TwoDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        case 3:
                            parserType = typeof(ThreeDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        case 4:
                            parserType = typeof(FourDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        case 5:
                            parserType = typeof(FiveDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        case 6:
                            parserType = typeof(SixDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        case 7:
                            parserType = typeof(SevenDimensionalArrayParser<>).MakeGenericType(type.GetElementType()!);
                            goto CREATE;
                        default:
                            return null; // not supported
                    }
                }
            }

            if (type.IsEnum || !typeIsReferenceOrContainsReferences)
            {
                parserType = typeof(DangerousUnmanagedParsers<>).MakeGenericType(type);
                goto CREATE;
            }

            parserType = TryCreateGenericParserType(type, _parsers);
            if (parserType != null) goto CREATE;
                
            return null;

            CREATE:
            return Activator.CreateInstance(parserType);
        }

        static Type? TryCreateGenericParserType(Type type, Dictionary<Type, Type> knownTypes)
        {
            if (type.IsGenericType)
            {
                var genericDefinition = type.GetGenericTypeDefinition();

                if (knownTypes.TryGetValue(genericDefinition, out var parserType))
                {
                    return parserType.MakeGenericType(type.GetGenericArguments());
                }
            }

            return null;
        }


        static class Check<T>
        {
            internal static volatile bool Registered;
            internal static ParserType ParserType { get; set; }
            
        }

        static class Cache<T>
        {
            internal static LuminPackParser<T>? parser;

            static Cache()
            { 
                if (Check<T>.Registered) return;

                try
                {
                    var type = typeof(T);

                    if (TryRegisterConstructor<T>())
                    {
                        return;
                    }
                    
                    var typeIsReferenceOrContainsReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
                    var f = CreateGenericParser(type, typeIsReferenceOrContainsReferences) as LuminPackParser<T>;

                    parser = f ?? new ErrorLuminPackParser<T>();
                }
                catch (Exception ex)
                {
                    parser = new ErrorLuminPackParser<T>(typeof(T), ex.Message);
                }
                
                Check<T>.ParserType = ParserType.Parsers;
                Check<T>.Registered = true;
            }
            
            
        }

        public enum ParserType : byte
        {
            NonRegistered,
            Data,
            Parsers
        }

    }

    [Preserve]
    internal sealed class ErrorLuminPackParser<T> : LuminPackParser<T>
    {
        readonly Type type;
        readonly string? message;

        public ErrorLuminPackParser()
        {
            type = typeof(T);
            message = null;
        }

        public ErrorLuminPackParser(Type type)
        {
            this.type = type;
            this.message = null;
        }

        public ErrorLuminPackParser(Type type, string message)
        {
            this.type = type;
            this.message = message;
        }

        public byte[] Serialize()
        {
            Throw();

            return null;
        }

        public override void Serialize(ref LuminPackWriter writer, scoped ref T? value)
        {
            Throw();
        }

        public override void Deserialize(ref LuminPackReader reader, scoped ref T? value)
        {
            Throw();
        }

        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value)
        {
            Throw();
        }

        public T Deserialize()
        {
            Throw();

            return default;
        }

        public T Deserialize(T result)
        {
            Throw();

            return default;
        }

        public Task<byte[]> SerializeAsync()
        {
            Throw();

            return null;
        }

        public Task<T> DeserializeAsync()
        {
            Throw();

            return null;
        }

        public Task<T> DeserializeAsync(T result)
        {
            Throw();

            return null;
        }

        [DoesNotReturn]
        private void Throw()
        {
            if (!string.IsNullOrEmpty(message))
            {
                LuminPackExceptionHelper.ThrowMessage(message);
            }
            else
            {
                LuminPackExceptionHelper.ThrowNoParserRegistered(type);
            }
        }
    }


    public static class ParserFactory
    {
        private static readonly ConcurrentDictionary<Type, Type> _parserCache = new ConcurrentDictionary<Type, Type>();
#if NET8_0_OR_GREATER
        private static System.Collections.Frozen.FrozenDictionary<Type, Type> ParserCache;
#else
        private static readonly ReadOnlyDictionary<Type, Type> ParserCache = new ReadOnlyDictionary<Type, Type>(_parserCache);
#endif
        private static readonly Assembly[] assemblies;

        static ParserFactory()
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        
        public static void Initialize()
        {
            
            foreach (var type in GetTypesWithAttribute<LuminPackableAttribute>())
            {
                
                var parserType = GetParserType(type);
                
                if (parserType != null)
                {
                    _parserCache[type] = parserType;
                }
            }

#if NET8_0_OR_GREATER
            ParserCache = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(_parserCache);
#endif
        }

        public static Type? GetParser(Type targetType)
        {
            if (targetType.IsGenericType)
            {
                if (_parserCache.TryGetValue(targetType.GetGenericTypeDefinition(), out var genericParser))
                {
                    return genericParser.MakeGenericType(targetType.GetGenericArguments());
                }
            }

            return ParserCache.GetValueOrDefault(targetType);
        }

        private static Type? GetParserType(Type originalType)
        {
            const string parserNamespace = "LuminPack.Generated";
            bool isGeneric = originalType.IsGenericType;

            string typeName;
            if (isGeneric)
            {
                Type genericDef = originalType.GetGenericTypeDefinition();
                string baseName = genericDef.Name.Split('`')[0];
                int argCount = genericDef.GetGenericArguments().Length;
                typeName = $"{parserNamespace}.{baseName}Parser`{argCount}";
            }
            else
            {
                typeName = $"{parserNamespace}.{originalType.Name}Parser";
            }
            
            
            // 仅在LuminPack程序集中查找
            Type? parserType = GetTypeWithName(typeName);
            

            return parserType;
        }

        private static IEnumerable<Type> GetTypesWithAttribute<TAttribute>() where TAttribute : System.Attribute
        {
            foreach (Assembly assembly in assemblies)
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (System.Attribute.IsDefined(type, typeof(TAttribute)))
                    {
                        yield return type;
                    }
                }
            }
        }
        
        private static Type? GetTypeWithName(string typeName)
        {
            Type? type;
            foreach (Assembly assembly in assemblies)
            {
                type = assembly.GetType(typeName);

                if (type != null)
                    return type;
            }
            
            return null;
        }
    }
}
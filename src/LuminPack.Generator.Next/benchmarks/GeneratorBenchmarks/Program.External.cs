using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace LuminPack.SourceGenerator.Next.Benchmarks;

internal static partial class Program
{
    /// <summary>Checks referenced types marked with <c>[assembly: LuminPackable(typeof(T))]</c> across two generated assemblies.</summary>
    /// <param name="references">The framework and runtime metadata.</param>
    /// <param name="parse">The consumer language and target settings.</param>
    /// <param name="coreDll">The runtime used to execute the consumer.</param>
    /// <returns>Zero when the consumer serializes the marked types exactly as their own assembly does.</returns>
    private static int VerifyExternal(MetadataReference[] references, CSharpParseOptions parse, string coreDll)
    {
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(coreDll));
        Compilation library = Generate("ExternalLibrary", ExternalLibrarySource, references, out _);
        byte[] image = Emit(library, new EmitOptions());
        byte[] metadata = Emit(library, new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        Assembly loaded = Assembly.Load(image);
        AppDomain.CurrentDomain.AssemblyResolve += (_, request) => new AssemblyName(request.Name).Name == "ExternalLibrary" ? loaded : null;

        // Complete implementation metadata: the consumer generates the marked types' operations, including private, internal and
        // automatic-property storage, and the imported Union writes through the library's own dispatch.
        Compilation consumer = Generate("ExternalConsumer", ExternalConsumerSource,
            references.Append(MetadataReference.CreateFromImage(image)).ToArray(), out Diagnostic[] diagnostics);
        RequireIncremental(diagnostics.All(item => item.Severity != DiagnosticSeverity.Error),
            string.Join(Environment.NewLine, diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        int checks = (int)Assembly.Load(Emit(consumer, new EmitOptions())).GetType("ExternalConsumer")!.GetMethod("Exercise")!.Invoke(null, null)!;

        // A reference assembly omits the backing storage of automatic properties, which cannot be inferred.
        Generate("ExternalReferenceConsumer", ExternalConsumerSource,
            references.Append(MetadataReference.CreateFromImage(metadata)).ToArray(), out diagnostics);
        string[] missing = diagnostics.Where(item => item.Id == "LuminPack112").Select(item => item.GetMessage()).ToArray();
        RequireIncremental(missing.Any(message => message.Contains("'Hidden'", StringComparison.Ordinal)), "LuminPack112 for Plain.Hidden.");
        RequireIncremental(missing.Any(message => message.Contains("'Nick'", StringComparison.Ordinal)), "LuminPack112 for Person.Nick.");
        Generate("ExternalOpenReferenceConsumer", OpenReferenceConsumerSource,
            references.Append(MetadataReference.CreateFromImage(metadata)).ToArray(), out diagnostics);
        missing = diagnostics.Where(item => item.Id == "LuminPack112").Select(item => item.GetMessage()).ToArray();
        RequireIncremental(missing.Length == 2 && missing.Any(message => message.Contains("AutoBox<int>", StringComparison.Ordinal)) &&
            missing.Any(message => message.Contains("AutoBox<string>", StringComparison.Ordinal)),
            "LuminPack112 once for each open-marker construction reached by direct calls and collection members.");
        Compilation options = Generate("ExternalOptionsConsumer", ExternalOptionsConsumerSource,
            references.Append(MetadataReference.CreateFromImage(image)).ToArray(), out _);
        checks += 3 + (int)Assembly.Load(Emit(options, new EmitOptions())).GetType("ExternalOptionsConsumer")!.GetMethod("Exercise")!.Invoke(null, null)!;
        Console.WriteLine($"Next external-root contracts: {checks} checks passed.");
        return 0;

        Compilation Generate(string name, string source, MetadataReference[] metadataReferences, out Diagnostic[] all)
        {
            var tree = CSharpSyntaxTree.ParseText(source, parse, name + ".cs");
            var input = CSharpCompilation.Create(name, new[] { tree }, metadataReferences,
                new CSharpCompilationOptions(name == "ExternalLibrary" ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication,
                    allowUnsafe: true, optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Enable));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new LuminPackSourceGenerator().AsSourceGenerator() }, parseOptions: parse)
                .RunGeneratorsAndUpdateCompilation(input, out var output, out var generated);
            foreach (var result in driver.GetRunResult().Results)
                RequireIncremental(result.Exception == null, result.Exception?.ToString() ?? string.Empty);
            all = generated.Concat(output.GetDiagnostics()).ToArray();
            return output;
        }

        static byte[] Emit(Compilation compilation, EmitOptions options)
        {
            using var stream = new MemoryStream();
            var emitted = compilation.Emit(stream, options: options);
            RequireIncremental(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            return stream.ToArray();
        }
    }

    private const string ExternalLibrarySource = """
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;

        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]

        namespace External
        {
            [LuminPackable]
            public partial class Person
            {
                public int Id;
                public string? Name;
                public Address Home;
                public string? Nick { get; set; }
                internal int Secret;
                [LuminPackInclude] private long _hidden;
                public long Hidden { get => _hidden; set => _hidden = value; }
            }

            [LuminPackable]
            public partial struct Address
            {
                public int Zip;
                public long Street;
            }

            [LuminPackable]
            [LuminPackUnion(0, typeof(Circle))]
            [LuminPackUnion(1, typeof(Square))]
            public abstract partial class Shape
            {
                public int Color;
            }

            [LuminPackable]
            public sealed partial class Circle : Shape
            {
                public double Radius;
            }

            [LuminPackable]
            public sealed partial class Square : Shape
            {
                public double Side;
                internal string? Label;
            }

            public class Plain
            {
                public int Number;
                public string? Text;
                public int Hidden { get; set; }
            }

            public class Box<T>
            {
                public T Value = default!;
                public int Number;
            }

            public class ExactBox<T>
            {
                public T Value = default!;
            }

            public class AutoBox<T>
            {
                public T Value { get; set; } = default!;
                [LuminPackIgnore] public T Ignored { get; set; } = default!;
            }

            [LuminPackable]
            [LuminPackUnion(7, typeof(Token<int>))]
            [LuminPackUnion(8, typeof(Token<string>))]
            [LuminPackUnion(9, typeof(IntToken))]
            public partial interface IToken<T> { }

            [LuminPackable]
            public sealed partial class IntToken : IToken<int>
            {
                public int Number;
            }

            [LuminPackable]
            public sealed partial class Token<T> : IToken<T>
            {
                public T Value = default!;
                internal int Number;
            }

            [LuminPackOption(StringRecording = LuminPackStringRecording.Token)]
            public class ScopedBox
            {
                public string? Value;
            }

            public static class Library
            {
                public static Person Person(int secret) => new Person { Id = 1, Name = "p", Home = new Address { Zip = 2, Street = 3 }, Nick = "n", Secret = secret, Hidden = 9 };
                public static int Secret(Person person) => person.Secret;
                public static Square Square(string label) => new Square { Color = 4, Side = 5, Label = label };
                public static string? Label(Square square) => square.Label;
                public static byte[] Write(Person person) => LuminPackSerializer.Serialize(person);
                public static byte[] Write(Shape shape) => LuminPackSerializer.Serialize(shape);
                public static Person ReadPerson(byte[] bytes) => LuminPackSerializer.Deserialize<Person>(bytes);
                public static Shape ReadShape(byte[] bytes) => LuminPackSerializer.Deserialize<Shape>(bytes);
                public static byte[] WriteToken(IToken<string> token) => LuminPackSerializer.Serialize(token);
                public static byte[] WriteTokenUtf8VarInt(IToken<string> token) => LuminPackSerializer.Serialize<IToken<string>, LuminPackUtf8VarIntOptions>(token);
                public static string WriteTokenJson(IToken<string> token) => LuminPackSerializer.SerializeJson(token);
                public static IToken<string> ReadToken(byte[] bytes) => LuminPackSerializer.Deserialize<IToken<string>>(bytes);
                public static Token<string> Token(string value, int number) => new Token<string> { Value = value, Number = number };
                public static int Number(Token<string> token) => token.Number;
            }
        }
        """;

    private const string ExternalConsumerSource = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using LuminPack;
        using LuminPack.Attribute;
        using External;

        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
        [assembly: LuminPackable(typeof(Plain))]
        [assembly: LuminPackable(typeof(Person))]
        [assembly: LuminPackable(typeof(Address))]
        [assembly: LuminPackable(typeof(Shape))]
        [assembly: LuminPackable(typeof(Box<>))]
        [assembly: LuminPackable(typeof(ExactBox<int>))]
        [assembly: LuminPackable(typeof(IToken<>))]
        [assembly: LuminPackable(typeof(IToken<int>))]
        [assembly: LuminPackable(typeof(AutoBox<>))]

        [LuminPackable]
        public partial class Order
        {
            public Person? Customer;
            public Plain? Note;
            public Address Ship;
            public List<Person>? Others;
            public List<Shape>? Shapes;
            public List<Box<string>>? Boxes;
            public List<IToken<string>>? Tokens;
        }

        public static class ExternalConsumer
        {
            public static void Main() => Exercise();

            public static int Exercise()
            {
                int checks = 0;
                void Check(bool condition, string name)
                {
                    if (!condition)
                        throw new InvalidOperationException("External root contract failed: " + name);
                    checks++;
                }

                Person person = Library.Person(secret: 11);
                byte[] own = LuminPackSerializer.Serialize(person);
                Check(own.SequenceEqual(Library.Write(person)), "marked record bytes equal the declaring assembly's");
                Person copy = LuminPackSerializer.Deserialize<Person>(own);
                Check(copy.Nick == "n" && Library.Secret(copy) == 11 && copy.Hidden == 9 && copy.Home.Street == 3, "marked record private, internal and property storage");
                Check(Library.ReadPerson(own).Nick == "n", "declaring assembly reads the consumer's bytes");

                Shape shape = Library.Square("l");
                byte[] shapeBytes = LuminPackSerializer.Serialize(shape);
                Check(shapeBytes.SequenceEqual(Library.Write(shape)), "imported Union bytes equal the declaring assembly's");
                var square = (Square)LuminPackSerializer.Deserialize<Shape>(shapeBytes);
                Check(square.Side == 5 && square.Color == 4 && Library.Label(square) == "l", "imported Union case storage");
                Check(((Square)Library.ReadShape(shapeBytes)).Side == 5, "declaring assembly reads the imported Union");

                var plain = new Plain { Number = 6, Text = "t", Hidden = 7 };
                Plain plainCopy = LuminPackSerializer.Deserialize<Plain>(LuminPackSerializer.Serialize(plain));
                Check(plainCopy.Hidden == 7 && plainCopy.Text == "t", "unattributed marked type");

                var box = new Box<int> { Value = 19, Number = 4 };
                Box<int> boxCopy = LuminPackSerializer.Deserialize<Box<int>>(LuminPackSerializer.Serialize(box));
                Check(boxCopy.Value == 19 && boxCopy.Number == 4, "open marker binds a closed binary root");
                var auto = new AutoBox<int> { Value = 21, Ignored = 99 };
                AutoBox<int> autoCopy = LuminPackSerializer.Deserialize<AutoBox<int>>(LuminPackSerializer.Serialize(auto));
                Check(autoCopy.Value == 21 && autoCopy.Ignored == 0, "open marker imports automatic storage from implementation metadata");
                var exact = new ExactBox<int> { Value = 13 };
                Check(LuminPackSerializer.DeserializeJson<ExactBox<int>>(LuminPackSerializer.SerializeJson(exact)).Value == 13,
                    "closed marker binds exactly one construction");
                try
                {
                    LuminPackSerializer.Serialize(new ExactBox<string> { Value = "unmarked" });
                    throw new InvalidOperationException("The closed marker must not bind another construction.");
                }
                catch (Exception error) when (error.GetType().Name == "LuminPackException")
                {
                    checks++;
                }

                var order = new Order { Customer = person, Note = plain, Ship = new Address { Zip = 8 }, Others = new List<Person> { person },
                    Shapes = new List<Shape> { shape, new Circle { Radius = 2 } },
                    Boxes = new List<Box<string>> { new Box<string> { Value = "box", Number = 9 } } };
                Order orderCopy = LuminPackSerializer.Deserialize<Order>(LuminPackSerializer.Serialize(order));
                Check(orderCopy.Others![0].Hidden == 9 && orderCopy.Ship.Zip == 8 && ((Circle)orderCopy.Shapes![1]).Radius == 2, "marked members of a local payload");
                Order json = LuminPackSerializer.DeserializeJson<Order>(LuminPackSerializer.SerializeJson(order));
                Check(json.Note!.Hidden == 7 && Library.Secret(json.Customer!) == 11 && ((Square)json.Shapes![0]).Side == 5, "marked members through JSON");
                Check(orderCopy.Boxes![0].Value == "box" && json.Boxes![0].Number == 9, "open marker binds an independently closed collection element");

                IToken<string> token = Library.Token("generic", 29);
                byte[] tokenBytes = LuminPackSerializer.Serialize(token);
                Check(tokenBytes.SequenceEqual(Library.WriteToken(token)), "imported generic Union writes through library slots");
                Check(Library.Number((Token<string>)LuminPackSerializer.Deserialize<IToken<string>>(tokenBytes)) == 29 &&
                    ((Token<string>)Library.ReadToken(tokenBytes)).Value == "generic", "closed generic Union case reads in both assemblies");
                string tokenJson = LuminPackSerializer.SerializeJson(token);
                Check(tokenJson == Library.WriteTokenJson(token) && ((Token<string>)LuminPackSerializer.DeserializeJson<IToken<string>>(tokenJson)).Value == "generic",
                    "imported generic Union JSON slots and cases");
                IToken<int> exactToken = new Token<int> { Value = 31 };
                Check(((Token<int>)LuminPackSerializer.Deserialize<IToken<int>>(LuminPackSerializer.Serialize(exactToken))).Value == 31,
                    "exact generic Union marker");
                exactToken = new IntToken { Number = 37 };
                byte[] exactBytes = LuminPackSerializer.Serialize(exactToken);
                Check(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(exactBytes) == 0xCF7289C2u && ((IntToken)LuminPackSerializer.Deserialize<IToken<int>>(exactBytes)).Number == 37,
                    "non-generic case implements the constructed generic interface slot");
                order.Tokens = new List<IToken<string>> { token };
                Check(((Token<string>)LuminPackSerializer.Deserialize<Order>(LuminPackSerializer.Serialize(order)).Tokens![0]).Value == "generic" &&
                    Library.Number((Token<string>)LuminPackSerializer.DeserializeJson<Order>(LuminPackSerializer.SerializeJson(order)).Tokens![0]) == 29,
                    "open imported Union marker reached by a collection member");
                return checks;
            }
        }
        """;

    private const string OpenReferenceConsumerSource = """
        using System.Collections.Generic;
        using External;
        using LuminPack;
        using LuminPack.Attribute;

        [assembly: LuminPackable(typeof(AutoBox<>))]

        [LuminPackable]
        public partial class ReferenceHolder
        {
            public List<AutoBox<string>>? Boxes;
        }

        public static class ExternalOpenReferenceConsumer
        {
            public static void Main()
            {
                LuminPackSerializer.Serialize(new AutoBox<int>());
                LuminPackSerializer.Deserialize<AutoBox<int>>(new byte[0]);
            }
        }
        """;

    private const string ExternalOptionsConsumerSource = """
        using System;
        using System.Linq;
        using External;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;

        [assembly: LuminPackGeneratorOptions(LuminPackRegisterMode.Disabled)]
        [assembly: LuminPackOption(StringEncoding = LuminPackStringEncoding.UTF8, StringRecording = LuminPackStringRecording.VarInt)]
        [assembly: LuminPackable(typeof(Person))]
        [assembly: LuminPackable(typeof(Address))]
        [assembly: LuminPackable(typeof(ScopedBox))]
        [assembly: LuminPackable(typeof(IToken<>))]
        [assembly: LuminPackable(typeof(Shape))]

        [LuminPackable, LuminPackOption(MaxStringLength = 2)]
        public sealed class LimitedImportedUnions
        {
            public Shape? Shape;
            public IToken<string>? Token;
            public System.Collections.Generic.List<Shape>? Shapes;
        }

        [LuminPackable]
        [LuminPackOption(StringRecording = LuminPackStringRecording.Token)]
        public partial class LocalBox
        {
            public string? Value;
        }

        public static class ExternalOptionsConsumer
        {
            public static void Main() => Exercise();

            public static int Exercise()
            {
                Person person = Library.Person(41);
                byte[] bytes = LuminPackSerializer.Serialize(person);
                if (bytes.SequenceEqual(Library.Write(person)) || LuminPackSerializer.Deserialize<Person>(bytes).Nick != "n")
                    throw new InvalidOperationException("A marked root must inherit the consumer assembly profile.");
                var box = new ScopedBox { Value = "\u4E2D" };
                byte[] scoped = LuminPackSerializer.Serialize(box);
                if (!scoped.SequenceEqual(LuminPackSerializer.Serialize(new LocalBox { Value = box.Value })) ||
                    LuminPackSerializer.Deserialize<ScopedBox>(scoped).Value != box.Value)
                    throw new InvalidOperationException("An imported type option must overlay the consumer assembly profile.");
                IToken<string> token = Library.Token("\u4E2D", 43);
                byte[] union = LuminPackSerializer.Serialize(token);
                if (!union.SequenceEqual(Library.WriteTokenUtf8VarInt(token)) || union.SequenceEqual(Library.WriteToken(token)) ||
                    ((Token<string>)LuminPackSerializer.Deserialize<IToken<string>>(union)).Value != "\u4E2D")
                    throw new InvalidOperationException("An imported Union must pass the consumer profile through the defining assembly's slots.");
                var limited = new LimitedImportedUnions { Shape = Library.Square("ok"), Token = token,
                    Shapes = new() { Library.Square("ok") } };
                var copy = LuminPackSerializer.Deserialize<LimitedImportedUnions>(LuminPackSerializer.Serialize(limited));
                if (Library.Label((Square)copy.Shape!) != "ok" || Library.Label((Square)copy.Shapes![0]) != "ok" ||
                    copy.Token is not Token<string> copiedToken || copiedToken.Value != "\u4e2d" || Library.Number(copiedToken) != 43)
                    throw new InvalidOperationException("A checked imported Union changed the case or its fields.");
                var existingShape = copy.Shape;
                var existingList = copy.Shapes;
                byte[] accepted = LuminPackSerializer.Serialize(limited);
                if (LuminPackSerializer.Deserialize(accepted, ref copy) != accepted.Length ||
                    !ReferenceEquals(existingShape, copy.Shape) || !ReferenceEquals(existingList, copy.Shapes))
                    throw new InvalidOperationException("A checked imported Union lost existing storage reuse.");
                limited.Shape = Library.Square("four");
                Reject(limited);
                limited.Shape = null;
                limited.Token = Library.Token("four", 43);
                Reject(limited);
                limited.Token = null;
                limited.Shapes![0] = Library.Square("four");
                Reject(limited);
                return 8;

                static void Reject(LimitedImportedUnions value)
                {
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    try { LuminPackSerializer.Deserialize<LimitedImportedUnions>(bytes); }
                    catch (System.IO.InvalidDataException error) when (error.Message.Contains("string length", StringComparison.Ordinal)) { return; }
                    throw new InvalidOperationException("An imported Union case bypassed the enclosing string limit.");
                }
            }
        }
        """;
}

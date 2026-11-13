using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator.Next.Pipeline;
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
        checks += 2;
        Console.WriteLine($"Next external-root contracts: {checks} checks passed.");
        return 0;

        Compilation Generate(string name, string source, MetadataReference[] metadataReferences, out Diagnostic[] all)
        {
            var tree = CSharpSyntaxTree.ParseText(source, parse, name + ".cs");
            var input = CSharpCompilation.Create(name, new[] { tree }, metadataReferences,
                new CSharpCompilationOptions(name == "ExternalLibrary" ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication,
                    allowUnsafe: true, optimizationLevel: OptimizationLevel.Release,
                    nullableContextOptions: NullableContextOptions.Enable));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new NextGenerator().AsSourceGenerator() }, parseOptions: parse)
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

        [LuminPackable]
        public partial class Order
        {
            public Person? Customer;
            public Plain? Note;
            public Address Ship;
            public List<Person>? Others;
            public List<Shape>? Shapes;
            public List<Box<string>>? Boxes;
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
                return checks;
            }
        }
        """;
}

// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Attribute;
using LuminPack.NinoBenchmark.Workloads;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.AppData
{

    // Each AppData scenario represents a web client's inputs as one array. The values use the original
    // TypeScript fixture's first variant (seed 42) in its original order.

    /// <summary>A value behind a type id: two levels below the base (Dog, then Puppy), and a sibling.</summary>
    [NinoType]
    [LuminPackable]
    [LuminPackUnion(0, typeof(Dog))]
    [LuminPackUnion(1, typeof(Puppy))]
    [LuminPackUnion(2, typeof(Cat))]
    public abstract partial class Animal
    {
        public string Name;
    }

    [NinoType]
    [LuminPackable]
    public partial class Dog : Animal
    {
        public int Bones;
    }

    [NinoType]
    [LuminPackable]
    public sealed partial class Puppy : Dog
    {
        public bool Sleepy;
    }

    [NinoType]
    [LuminPackable]
    public sealed partial class Cat : Animal
    {
        public float Lives;
    }

    /// <summary>A typical record: numbers, short text, a small array, a map and a subtype behind a type id.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class Player
    {
        public long Id;
        public string Name;
        public int Level;
        public double Score;
        public bool Online;
        public Vec3 Position;
        public int[] Unlocked;
        public Dictionary<string, int> Inventory;
        public Animal Pet;
    }

    /// <summary>Values behind a type id.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class Zoo
    {
        public List<Animal> Animals;
    }

    [NinoType]
    [LuminPackable]
    public sealed partial class Order
    {
        public int Id;
        public string Sku;
        public string Customer;
        public int Quantity;
        public double Price;
        public bool Paid;
        public byte Status;
    }

    /// <summary>A list of small records.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class OrderBook
    {
        public List<Order> Orders;
    }

    /// <summary>Mostly text, some of it not Latin-1.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class Article
    {
        public string Title;
        public string Author;
        public string Body;
        public string Summary;
        public List<string> Tags;
        public int Views;
    }

    /// <summary>Arrays of small structs and numbers.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class Mesh
    {
        public string Name;
        public Vec3[] Vertices;
        public Vec3[] Normals;
        public int[] Indices;
    }

    [NinoType]
    [LuminPackable]
    public sealed partial class Samples
    {
        public double[] Values;
        public int[] Counts;
        public float[] Weights;
    }

    /// <summary>Maps keyed and valued by short text.</summary>
    [NinoType]
    [LuminPackable]
    public sealed partial class Lookup
    {
        public Dictionary<string, int> Counts;
        public Dictionary<int, string> Names;
    }

    [NinoType]
    [LuminPackable]
    public sealed partial class Event
    {
        public long At;
        public long UserId;
        public int Kind;
        public long[] Refs;
    }

    /// <summary>Each scenario's values, as the TypeScript benchmark's first variant makes them.</summary>
    public static class AppDataValues
    {
        public static readonly Player[] Players;
        public static readonly Zoo[] Zoos;
        public static readonly OrderBook[] OrderBooks;
        public static readonly Article[] Articles;
        public static readonly Mesh[] Meshes;
        public static readonly Samples[] SampleSets;
        public static readonly Lookup[] Lookups;
        public static readonly Event[] Events;

        /// <summary>Initializes all eight AppData scenarios with the same seed.</summary>
        static AppDataValues()
        {
            var rng = new Random(42);
            string Word(int n) => new string(Enumerable.Range(0, n).Select(_ => (char)('a' + rng.Next(26))).ToArray());
            string Sentence(int words) => string.Join(" ", Enumerable.Range(0, words).Select(_ => Word(rng.Next(2, 10))));
            Vec3 V() => new() { X = (float)rng.NextDouble(), Y = (float)rng.NextDouble(), Z = (float)rng.NextDouble() };
            Animal Pet(int i) => (i % 3) switch
            {
                0 => new Dog { Name = "pet" + i, Bones = i },
                1 => new Cat { Name = "pet" + i, Lives = 9 },
                _ => new Puppy { Name = "pet" + i, Bones = i, Sleepy = i % 2 == 0 },
            };

            Players = Enumerable.Range(0, 1000).Select(i => new Player
            {
                Id = 1_000_000_000L + i, Name = "Player " + i, Level = rng.Next(100), Score = rng.NextDouble() * 1e4,
                Online = i % 2 == 0, Position = V(), Unlocked = Enumerable.Range(0, 8).Select(_ => rng.Next(1000)).ToArray(),
                Inventory = new Dictionary<string, int> { ["sword"] = rng.Next(5), ["shield"] = rng.Next(5) }, Pet = Pet(i),
            }).ToArray();
            Zoos = Enumerable.Range(0, 50).Select(i => new Zoo
            {
                Animals = Enumerable.Range(0, 100).Select(k => Pet(i + k)).ToList(),
            }).ToArray();
            OrderBooks = Enumerable.Range(0, 100).Select(b => new OrderBook
            {
                Orders = Enumerable.Range(0, 50).Select(i => new Order
                {
                    Id = b * 1000 + i, Sku = "SKU-" + rng.Next(100000).ToString("D6"), Customer = Word(6) + " " + Word(8),
                    Quantity = rng.Next(1, 20), Price = Math.Round(rng.NextDouble() * 1000, 2), Paid = rng.Next(2) == 0,
                    Status = (byte)rng.Next(4),
                }).ToList(),
            }).ToArray();
            Articles = Enumerable.Range(0, 200).Select(i => new Article
            {
                Title = Sentence(8), Author = Word(5) + " " + Word(7), Body = Sentence(300),
                Summary = i % 2 == 0
                    ? "\u6570\u636E\u5E8F\u5217\u5316\u7684\u6027\u80FD\u6D4B\u8BD5\uFF0C\u5305\u542B\u4E2D\u6587\u6587\u672C\u3002" + Sentence(5)
                    : "Caf\u00E9 cr\u00E8me \u2014 r\u00E9sum\u00E9 na\u00EFve " + Sentence(10),
                Tags = Enumerable.Range(0, 5).Select(_ => Word(6)).ToList(), Views = rng.Next(),
            }).ToArray();
            Meshes = Enumerable.Range(0, 20).Select(i => new Mesh
            {
                Name = "mesh" + i,
                Vertices = Enumerable.Range(0, 500).Select(_ => V()).ToArray(),
                Normals = Enumerable.Range(0, 500).Select(_ => V()).ToArray(),
                Indices = Enumerable.Range(0, 1500).Select(_ => rng.Next(500)).ToArray(),
            }).ToArray();
            SampleSets = Enumerable.Range(0, 20).Select(_ => new Samples
            {
                Values = Enumerable.Range(0, 1000).Select(_ => rng.NextDouble() * 1e6).ToArray(),
                Counts = Enumerable.Range(0, 1000).Select(_ => rng.Next(-1000000, 1000000)).ToArray(),
                Weights = Enumerable.Range(0, 1000).Select(_ => (float)rng.NextDouble()).ToArray(),
            }).ToArray();
            Lookups = Enumerable.Range(0, 50).Select(_ => new Lookup
            {
                Counts = Enumerable.Range(0, 100).ToDictionary(i => "key_" + Word(6) + i, _ => rng.Next(100000)),
                Names = Enumerable.Range(0, 100).ToDictionary(i => i * 7, _ => Word(10)),
            }).ToArray();
            Events = Enumerable.Range(0, 1000).Select(i => new Event
            {
                At = 1760790645123L * 10000 + i, UserId = rng.NextInt64(1L << 40), Kind = rng.Next(10),
                Refs = Enumerable.Range(0, 4).Select(_ => rng.NextInt64()).ToArray(),
            }).ToArray();
        }
    }
}

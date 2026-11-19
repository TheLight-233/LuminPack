// Adapted from the Nino benchmark suite. See LICENSE.Nino.txt.
using System;
using LuminPack.Attribute;
using Nino.Core;

#nullable disable
namespace LuminPack.NinoBenchmark.Workloads
{

    /// <summary>An engine's own vector type (game engines bring their own math types).</summary>
    [NinoType, LuminPackable]
    public partial struct Vec3
    {
        public float X;
        public float Y;
        public float Z;
    }

    [NinoType, LuminPackable]
    public partial struct Quat
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
    }

    public static class Data
    {
        public static readonly DateTime Epoch = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] First = {"Jane", "Wei", "Amara", "Lucas", "Priya", "Kenji", "Sofia", "Omar"};
        private static readonly string[] Last = {"Doe", "Zhang", "Okafor", "Silva", "Sharma", "Sato", "Rossi", "Haddad"};

        /// <summary>Chooses a first and last name for a fixture.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The combined name.</returns>
        public static string Name(Random r) => First[r.Next(First.Length)] + " " + Last[r.Next(Last.Length)];

        /// <summary>Creates an identifier from the fixture's random stream.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated identifier.</returns>
        public static Guid NewGuid(Random r)
        {
            Span<byte> bytes = stackalloc byte[16];
            r.NextBytes(bytes);
            return new Guid(bytes);
        }

        /// <summary>Creates a vector whose signed components are bounded by the supplied scale.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <param name="scale">The maximum absolute component value.</param>
        /// <returns>The generated vector.</returns>
        public static Vec3 V(Random r, float scale) =>
            new() { X = (r.NextSingle() * 2 - 1) * scale, Y = (r.NextSingle() * 2 - 1) * scale, Z = (r.NextSingle() * 2 - 1) * scale };

        /// <summary>Creates a quaternion fixture with two varying components.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <returns>The generated quaternion.</returns>
        public static Quat Q(Random r) => new() { X = 0, Y = r.NextSingle(), Z = 0, W = r.NextSingle() };

        /// <summary>A paragraph of HTML, as a Q&amp;A site stores a post's body.</summary>
        /// <param name="r">The seeded random number generator.</param>
        /// <param name="sentences">The number of sentences to generate.</param>
        /// <returns>The generated HTML.</returns>
        public static string Html(Random r, int sentences)
        {
            var words = new[] { "serializer", "buffer", "the", "async", "value", "returns", "null", "when", "list", "type",
                "generic", "method", "this", "struct", "because", "allocation", "call", "string", "faster", "cache" };
            var text = new System.Text.StringBuilder("<p>");
            for (int s = 0; s < sentences; s++)
            {
                int n = r.Next(8, 16);
                for (int w = 0; w < n; w++) text.Append(w == 0 ? "" : " ").Append(words[r.Next(words.Length)]);
                text.Append(s % 3 == 2 ? ".</p>\n<p>" : ". ");
            }

            return text.Append("<code>var x = Serializer.Deserialize&lt;T&gt;(bytes);</code></p>").ToString();
        }
    }
}

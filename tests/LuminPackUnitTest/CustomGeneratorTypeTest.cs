using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>
    /// [LuminPackable(GeneratorType.Custom)] types emit no generated formatters; the user
    /// writes static Serialize/Deserialize methods and the source generator registers them
    /// automatically via LuminPackSerializer.Register through a module initializer.
    /// </summary>
    public class CustomGeneratorTypeTest
    {
        // ---- Tier 1: binary only ----
        /// <summary>Provides custom binary type functionality.</summary>
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomBinaryType
        {
            /// <summary>Stores the id.</summary>
            public int Id;
            /// <summary>Stores the name.</summary>
            public string Name = "";

            /// <summary>Serializes the supplied value using the selected format and options.</summary>
            /// <param name="writer">The writer receiving the output data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Serialize(ref LuminPackWriter writer, in CustomBinaryType value)
            {
                writer.WriteValue(value.Id);
                writer.WriteValue(value.Name);
            }

            /// <summary>Deserializes the supplied input using the selected format and options.</summary>
            /// <param name="reader">The reader supplying the input data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Deserialize(ref LuminPackReader reader, ref CustomBinaryType value)
            {
                value = new CustomBinaryType();
                reader.ReadValue(ref value.Id);
                reader.ReadValue(ref value.Name);
            }
        }

        // ---- Tier 2: binary + JSON ----
        /// <summary>Provides custom json type functionality.</summary>
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomJsonType
        {
            /// <summary>Stores the number.</summary>
            public int Number;
            /// <summary>Stores the text.</summary>
            public string Text = "";

            /// <summary>Serializes the supplied value using the selected format and options.</summary>
            /// <param name="writer">The writer receiving the output data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Serialize(ref LuminPackWriter writer, in CustomJsonType value)
            {
                writer.WriteValue(value.Number);
                writer.WriteValue(value.Text);
            }

            /// <summary>Deserializes the supplied input using the selected format and options.</summary>
            /// <param name="reader">The reader supplying the input data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Deserialize(ref LuminPackReader reader, ref CustomJsonType value)
            {
                value = new CustomJsonType();
                reader.ReadValue(ref value.Number);
                reader.ReadValue(ref value.Text);
            }

            /// <summary>Serializes the supplied value using the selected format and options.</summary>
            /// <param name="writer">The writer receiving the output data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomJsonType value)
            {
                writer.WriteObjectStart();
                writer.WritePropertyName("Number");
                writer.WriteValue(value.Number);
                writer.WritePropertyName("Text");
                writer.WriteValue(value.Text);
                writer.WriteObjectEnd();
            }

            /// <summary>Deserializes the supplied input using the selected format and options.</summary>
            /// <param name="reader">The reader supplying the input data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void DeserializeJson(ref LuminPackJsonReader reader, ref CustomJsonType value)
            {
                value = new CustomJsonType();
                if (reader.IsNull())
                {
                    value = null!;
                    return;
                }
                reader.TryConsumeObjectStart();
                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd) break;
                    if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
                    {
                        reader.Skip();
                        continue;
                    }
                    var property = reader.ReadString();
                    if (!reader.Read()) break;
                    if (property == "Number") value.Number = reader.ReadInt();
                    else if (property == "Text") value.Text = reader.ReadString();
                    else reader.Skip();
                }
            }
        }

        // ---- Tier 3: binary + JSON + calculate ----
        /// <summary>Provides custom full type functionality.</summary>
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomFullType
        {
            /// <summary>Stores the a.</summary>
            public long A;

            /// <summary>Serializes the supplied value using the selected format and options.</summary>
            /// <param name="writer">The writer receiving the output data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Serialize(ref LuminPackWriter writer, in CustomFullType value)
            {
                writer.WriteValue(value.A);
            }

            /// <summary>Deserializes the supplied input using the selected format and options.</summary>
            /// <param name="reader">The reader supplying the input data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void Deserialize(ref LuminPackReader reader, ref CustomFullType value)
            {
                value = new CustomFullType();
                reader.ReadValue(ref value.A);
            }

            /// <summary>Serializes the supplied value using the selected format and options.</summary>
            /// <param name="writer">The writer receiving the output data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomFullType value)
            {
                writer.WriteObjectStart();
                writer.WritePropertyName("A");
                writer.WriteValue(value.A);
                writer.WriteObjectEnd();
            }

            /// <summary>Deserializes the supplied input using the selected format and options.</summary>
            /// <param name="reader">The reader supplying the input data.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void DeserializeJson(ref LuminPackJsonReader reader, ref CustomFullType value)
            {
                value = new CustomFullType();
                if (reader.IsNull())
                {
                    value = null!;
                    return;
                }
                reader.TryConsumeObjectStart();
                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd) break;
                    if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
                    {
                        reader.Skip();
                        continue;
                    }
                    var property = reader.ReadString();
                    if (!reader.Read()) break;
                    if (property == "A") value.A = reader.ReadLong();
                    else reader.Skip();
                }
            }

            /// <summary>Calculates the serialized size contribution of offset.</summary>
            /// <param name="evaluator">The evaluator used by this operation.</param>
            /// <param name="value">The value consumed or produced by this operation.</param>
            public static void CalculateOffset(ref LuminPackEvaluator evaluator, in CustomFullType value)
            {
                evaluator.Add(8);
            }
        }

        /// <summary>Verifies custom binary round trips.</summary>
        [Fact]
        public void CustomBinaryRoundTrips()
        {
            var value = new CustomBinaryType { Id = 42, Name = "custom" };

            byte[] buffer = LuminPackSerializer.Serialize(value);
            var result = LuminPackSerializer.Deserialize<CustomBinaryType>(buffer);

            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("custom", result.Name);
        }

        /// <summary>Verifies custom json round trips.</summary>
        [Fact]
        public void CustomJsonRoundTrips()
        {
            var value = new CustomJsonType { Number = 99, Text = "json" };

            string json = LuminPackSerializer.SerializeJson(value);
            var result = LuminPackSerializer.DeserializeJson<CustomJsonType>(json);

            Assert.NotNull(result);
            Assert.Equal(99, result.Number);
            Assert.Equal("json", result.Text);
        }

        /// <summary>Verifies custom full tier round trips binary and json.</summary>
        [Fact]
        public void CustomFullTierRoundTripsBinaryAndJson()
        {
            var value = new CustomFullType { A = 1234567890123L };

            byte[] buffer = LuminPackSerializer.Serialize(value);
            var result = LuminPackSerializer.Deserialize<CustomFullType>(buffer);
            Assert.Equal(1234567890123L, result.A);

            string json = LuminPackSerializer.SerializeJson(value);
            var jsonResult = LuminPackSerializer.DeserializeJson<CustomFullType>(json);
            Assert.Equal(1234567890123L, jsonResult.A);
        }

        /// <summary>Verifies custom type used as lumin packable object field.</summary>
        [Fact]
        public void CustomTypeUsedAsLuminPackableObjectField()
        {
            // A Custom type referenced as a [LuminPackableObject] field dispatches through
            // the registered formatter instead of an inline generated graph.
            var owner = new CustomOwner { Payload = new CustomBinaryType { Id = 5, Name = "nested" } };

            byte[] buffer = LuminPackSerializer.Serialize(owner);
            var result = LuminPackSerializer.Deserialize<CustomOwner>(buffer);

            Assert.NotNull(result.Payload);
            Assert.Equal(5, result.Payload.Id);
            Assert.Equal("nested", result.Payload.Name);
        }

        /// <summary>Provides custom owner functionality.</summary>
        [LuminPackable]
        public sealed class CustomOwner
        {
            /// <summary>Stores the payload.</summary>
            [LuminPackOrder(0)]
            [LuminPackableObject]
            public CustomBinaryType Payload = new();
        }
    }
}

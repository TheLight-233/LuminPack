using System;
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
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomBinaryType
        {
            public int Id;
            public string Name = "";

            public static void Serialize(ref LuminPackWriter writer, in CustomBinaryType value)
            {
                writer.WriteValue(value.Id);
                writer.WriteValue(value.Name);
            }

            public static void Deserialize(ref LuminPackReader reader, ref CustomBinaryType value)
            {
                value = new CustomBinaryType();
                reader.ReadValue(ref value.Id);
                reader.ReadValue(ref value.Name);
            }
        }

        // ---- Tier 2: binary + JSON ----
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomJsonType
        {
            public int Number;
            public string Text = "";

            public static void Serialize(ref LuminPackWriter writer, in CustomJsonType value)
            {
                writer.WriteValue(value.Number);
                writer.WriteValue(value.Text);
            }

            public static void Deserialize(ref LuminPackReader reader, ref CustomJsonType value)
            {
                value = new CustomJsonType();
                reader.ReadValue(ref value.Number);
                reader.ReadValue(ref value.Text);
            }

            public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomJsonType value)
            {
                writer.WriteObjectStart();
                writer.WritePropertyName("Number");
                writer.WriteValue(value.Number);
                writer.WritePropertyName("Text");
                writer.WriteValue(value.Text);
                writer.WriteObjectEnd();
            }

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
        [LuminPackable(GeneratorType.Custom)]
        public sealed class CustomFullType
        {
            public long A;

            public static void Serialize(ref LuminPackWriter writer, in CustomFullType value)
            {
                writer.WriteValue(value.A);
            }

            public static void Deserialize(ref LuminPackReader reader, ref CustomFullType value)
            {
                value = new CustomFullType();
                reader.ReadValue(ref value.A);
            }

            public static void SerializeJson(ref LuminPackJsonWriter writer, in CustomFullType value)
            {
                writer.WriteObjectStart();
                writer.WritePropertyName("A");
                writer.WriteValue(value.A);
                writer.WriteObjectEnd();
            }

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

            public static void CalculateOffset(ref LuminPackEvaluator evaluator, in CustomFullType value)
            {
                evaluator.Add(8);
            }
        }

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

        [LuminPackable]
        public sealed class CustomOwner
        {
            [LuminPackOrder(0)]
            [LuminPackableObject]
            public CustomBinaryType Payload = new();
        }
    }
}

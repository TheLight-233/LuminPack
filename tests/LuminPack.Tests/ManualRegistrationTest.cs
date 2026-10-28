using LuminPack;
using LuminPack.Core;
using LuminPack.Generated;

namespace LuminPackUnitTest
{
    /// <summary>
    /// Verifies the manual registration fallback: a type the source generator does NOT cover
    /// (no [LuminPackable], not referenced by any generated formatter) has no generated TypeId, so
    /// the generic dispatch's default branch consults <see cref="LuminPack.Code.LuminPackFormatterRegistry"/>
    /// and calls the registered static method pointer directly (typed signature, no boxing).
    /// </summary>
    public static class ManualRegistrationTest
    {
        /// <summary>Verifies run.</summary>
        /// <param name="results">The results used by this operation.</param>
        public static void Run(List<string> results)
        {
            RegisterAll();
            RunCase(results, nameof(ManualRegistrationRoundTripsBinary), ManualRegistrationRoundTripsBinary);
            RunCase(results, nameof(ManualRegistrationRoundTripsJson), ManualRegistrationRoundTripsJson);
            RunCase(results, nameof(DuplicateRegistrationThrows), DuplicateRegistrationThrows);
        }

        /// <summary>Provides manual registered type functionality.</summary>
        public sealed class ManualRegisteredType
        {
            /// <summary>Stores the id.</summary>
            public int Id;
            /// <summary>Stores the name.</summary>
            public string Name = "";
        }

        /// <summary>Writes manual to the current output.</summary>
        /// <param name="writer">The writer receiving the output data.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private static void WriteManual(ref LuminPackWriter writer, in ManualRegisteredType value)
        {
            writer.Write(value.Id);
            writer.Write(value.Name);
        }

        /// <summary>Reads manual from the current input.</summary>
        /// <param name="reader">The reader supplying the input data.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private static void ReadManual(ref LuminPackReader reader, ref ManualRegisteredType value)
        {
            value = new ManualRegisteredType();
            reader.Read(ref value.Id);
            reader.Read(ref value.Name);
        }

        /// <summary>Writes manual json to the current output.</summary>
        /// <param name="writer">The writer receiving the output data.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private static void WriteManualJson(ref LuminPackJsonWriter writer, in ManualRegisteredType value)
        {
            writer.WriteObjectStart();
            writer.WritePropertyName("Id");
            writer.Write(value.Id);
            writer.WritePropertyName("Name");
            writer.Write(value.Name);
            writer.WriteObjectEnd();
        }

        /// <summary>Reads manual json from the current input.</summary>
        /// <param name="reader">The reader supplying the input data.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        private static void ReadManualJson(ref LuminPackJsonReader reader, ref ManualRegisteredType value)
        {
            value = new ManualRegisteredType();
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
                var property = reader.Read<string, global::LuminPack.Option.LuminPackDefaultOptions>();
                if (!reader.Read()) break;
                if (property == "Id") value.Id = reader.Read<int, global::LuminPack.Option.LuminPackDefaultOptions>();
                else if (property == "Name") value.Name = reader.Read<string, global::LuminPack.Option.LuminPackDefaultOptions>();
                else reader.Skip();
            }
        }

        /// <summary>Verifies manual registration round trips binary.</summary>
        private static void ManualRegistrationRoundTripsBinary()
        {
            var original = new ManualRegisteredType { Id = 42, Name = "manual-binary" };
            byte[] payload = LuminPackSerializer.Serialize(original);
            var back = LuminPackSerializer.Deserialize<ManualRegisteredType>(payload);
            Assert(back.Id == 42 && back.Name == "manual-binary",
                "Manual binary round-trip produced different values.");
        }

        /// <summary>Verifies manual registration round trips json.</summary>
        private static void ManualRegistrationRoundTripsJson()
        {
            var original = new ManualRegisteredType { Id = 7, Name = "manual-json" };
            string json = LuminPackSerializer.SerializeJson(original);
            var back = LuminPackSerializer.DeserializeJson<ManualRegisteredType>(json);
            Assert(back.Id == 7 && back.Name == "manual-json",
                "Manual JSON round-trip produced different values.");
        }

        /// <summary>Verifies duplicate registration throws.</summary>
        private static void DuplicateRegistrationThrows()
        {
            bool threw = false;
            unsafe
            {
                try
                {
                    LuminPackSerializer.Register<ManualRegisteredType>(&WriteManual, &ReadManual);
                }
                catch (ArgumentException)
                {
                    threw = true;
                }
            }
            Assert(threw, "Registering the same type twice did not throw ArgumentException.");
        }

        /// <summary>Verifies register all.</summary>
        private static unsafe void RegisterAll()
        {
            LuminPackSerializer.Register<ManualRegisteredType>(&WriteManual, &ReadManual, &WriteManualJson, &ReadManualJson);
        }

        /// <summary>Verifies run case.</summary>
        /// <param name="results">The results used by this operation.</param>
        /// <param name="name">The name used by this operation.</param>
        /// <param name="test">The test used by this operation.</param>
        private static void RunCase(List<string> results, string name, Action test)
        {
            try
            {
                test();
                results.Add("✓ " + name + " - PASSED");
            }
            catch (Exception ex)
            {
                results.Add("✗ " + name + " - ERROR: " + ex.Message);
            }
        }

        /// <summary>Verifies assert.</summary>
        /// <param name="condition">The condition used by this operation.</param>
        /// <param name="message">The message describing the failure.</param>
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

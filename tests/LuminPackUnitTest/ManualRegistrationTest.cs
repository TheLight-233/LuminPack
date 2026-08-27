using System;
using System.Collections.Generic;
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
        public static void Run(List<string> results)
        {
            RegisterAll();
            RunCase(results, nameof(ManualRegistrationRoundTripsBinary), ManualRegistrationRoundTripsBinary);
            RunCase(results, nameof(ManualRegistrationRoundTripsJson), ManualRegistrationRoundTripsJson);
            RunCase(results, nameof(DuplicateRegistrationThrows), DuplicateRegistrationThrows);
        }

        public sealed class ManualRegisteredType
        {
            public int Id;
            public string Name = "";
        }

        private static void WriteManual(ref LuminPackWriter writer, in ManualRegisteredType value)
        {
            writer.WriteValue(value.Id);
            writer.WriteValue(value.Name);
        }

        private static void ReadManual(ref LuminPackReader reader, ref ManualRegisteredType value)
        {
            value = new ManualRegisteredType();
            reader.ReadValue(ref value.Id);
            reader.ReadValue(ref value.Name);
        }

        private static void WriteManualJson(ref LuminPackJsonWriter writer, in ManualRegisteredType value)
        {
            writer.WriteObjectStart();
            writer.WritePropertyName("Id");
            writer.WriteValue(value.Id);
            writer.WritePropertyName("Name");
            writer.WriteValue(value.Name);
            writer.WriteObjectEnd();
        }

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
                var property = reader.ReadString();
                if (!reader.Read()) break;
                if (property == "Id") value.Id = reader.ReadInt();
                else if (property == "Name") value.Name = reader.ReadString();
                else reader.Skip();
            }
        }

        private static void ManualRegistrationRoundTripsBinary()
        {
            var original = new ManualRegisteredType { Id = 42, Name = "manual-binary" };
            byte[] payload = LuminPackSerializer.Serialize(original);
            var back = LuminPackSerializer.Deserialize<ManualRegisteredType>(payload);
            Assert(back.Id == 42 && back.Name == "manual-binary",
                "Manual binary round-trip produced different values.");
        }

        private static void ManualRegistrationRoundTripsJson()
        {
            var original = new ManualRegisteredType { Id = 7, Name = "manual-json" };
            string json = LuminPackSerializer.SerializeJson(original);
            var back = LuminPackSerializer.DeserializeJson<ManualRegisteredType>(json);
            Assert(back.Id == 7 && back.Name == "manual-json",
                "Manual JSON round-trip produced different values.");
        }

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

        private static unsafe void RegisterAll()
        {
            LuminPackSerializer.Register<ManualRegisteredType>(&WriteManual, &ReadManual, &WriteManualJson, &ReadManualJson);
        }

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

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
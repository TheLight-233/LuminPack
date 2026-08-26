using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using LuminPack;
using LuminPack.Attribute;

namespace LuminPackUnitTest
{
    [LuminPackable]
    public partial class IBufferModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<int> Numbers { get; set; } = new();
    }

    public static class IBufferWriterInteropTest
    {
        public static void Run(List<string> results)
        {
            var value = new IBufferModel { Id = 5, Name = "pipe", Numbers = new List<int> { 1, 2, 3 } };

            // 1. ArrayBufferWriter<byte>
            try
            {
                var writer = new ArrayBufferWriter<byte>();
                LuminPackSerializer.Serialize(value, writer);
                var back = LuminPackSerializer.Deserialize<IBufferModel>(writer.WrittenSpan);
                results.Add(EqualsModel(value, back)
                    ? "✓ IBufferWriter ArrayBufferWriter - PASSED"
                    : "✗ IBufferWriter ArrayBufferWriter - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ IBufferWriter ArrayBufferWriter - ERROR: {ex.Message}");
            }

            // 2. PipeWriter (System.IO.Pipelines) — an IBufferWriter<byte> from the Pipeline API
            try
            {
                var pipe = new Pipe();
                LuminPackSerializer.Serialize(value, pipe.Writer);
                pipe.Writer.Complete();
                var readResult = pipe.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
                var back2 = LuminPackSerializer.Deserialize<IBufferModel>(readResult.Buffer);
                results.Add(EqualsModel(value, back2)
                    ? "✓ IBufferWriter PipeWriter - PASSED"
                    : "✗ IBufferWriter PipeWriter - FAILED");
                pipe.Reader.Complete();
            }
            catch (Exception ex)
            {
                results.Add($"✗ IBufferWriter PipeWriter - ERROR: {ex.Message}");
            }

            // 3. SerializeJson into IBufferWriter
            try
            {
                var writer = new ArrayBufferWriter<byte>();
                LuminPackSerializer.SerializeJson(value, writer);
                var json = Encoding.UTF8.GetString(writer.WrittenSpan);
                var back3 = LuminPackSerializer.DeserializeJson<IBufferModel>(json);
                results.Add(EqualsModel(value, back3)
                    ? "✓ IBufferWriter SerializeJson - PASSED"
                    : "✗ IBufferWriter SerializeJson - FAILED");
            }
            catch (Exception ex)
            {
                results.Add($"✗ IBufferWriter SerializeJson - ERROR: {ex.Message}");
            }
        }

        private static bool EqualsModel(IBufferModel a, IBufferModel b)
            => a.Id == b.Id && a.Name == b.Name && a.Numbers.Count == b.Numbers.Count &&
               a.Numbers[0] == b.Numbers[0] && a.Numbers[1] == b.Numbers[1] && a.Numbers[2] == b.Numbers[2];
    }
}
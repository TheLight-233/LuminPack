using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>
    /// xUnit runner over the same shared test methods the console <c>Program.Main</c> uses.
    /// Console mode runs the full suite (dotnet run); xUnit mode (dotnet test) runs the same
    /// suite one test case per logical group, asserting every collected result passed.
    /// </summary>
    public class LuminPackXUnitTests
    {
        private static List<string> Run(Action<List<string>> runner)
        {
            var results = new List<string>();
            runner(results);
            return results;
        }

        private static void AssertSuitePassed(List<string> results)
        {
            var failures = results
                .Where(static result => !result.StartsWith("✓"))
                .ToArray();
            Assert.True(failures.Length == 0,
                "Suite failures (" + failures.Length + "):\n" + string.Join("\n", failures));
        }

        [Fact]
        public void CoreSerializationSuite()
            => AssertSuitePassed(Run(Program.RunCoreTests));

        [Fact]
        public void BufferWriterSerialization()
            => AssertSuitePassed(Run(BufferWriterSerializationTest.Run));

        [Fact]
        public void JsonWriterSafety()
            => AssertSuitePassed(Run(JsonWriterSafetyTest.Run));

        [Fact]
        public void JsonReaderSafety()
            => AssertSuitePassed(Run(JsonReaderSafetyTest.Run));

        [Fact]
        public void CompressionSafetyPerf()
            => AssertSuitePassed(Run(CompressionSafetyPerfTest.Run));

        [Fact]
        public void ReaderBoundaryValidation()
            => AssertSuitePassed(Run(ReaderBoundaryValidationTest.Run));

        [Fact]
        public void QueueStackRegression()
            => AssertSuitePassed(Run(QueueStackRegressionTest.Run));

        [Fact]
        public void AsyncSerializerRegression()
            => AssertSuitePassed(Run(AsyncSerializerRegressionTest.Run));

        [Fact]
        public void SerializerReentrancy()
            => AssertSuitePassed(Run(SerializerReentrancyTest.Run));

        [Fact]
        public void BinaryUnionSafety()
            => AssertSuitePassed(Run(BinaryUnionSafetyTest.Run));

        [Fact]
        public void BinaryDirectResultRegression()
            => AssertSuitePassed(Run(BinaryDirectResultRegressionTest.Run));

        [Fact]
        public void ConfirmedCorrectnessRegression()
            => AssertSuitePassed(Run(ConfirmedCorrectnessRegressionTest.Run));

        [Fact]
        public void ObjectPoolRegression()
            => AssertSuitePassed(Run(ObjectPoolRegressionTest.Run));

        [Fact]
        public void LuminBufferWriterPoolRegression()
            => AssertSuitePassed(Run(LuminBufferWriterPoolRegressionTest.Run));

        [Fact]
        public void BufferWriterOperationContext()
            => AssertSuitePassed(Run(BufferWriterOperationContextTest.Run));

        [Fact]
        public void WriterReaderCorrectnessRegression()
            => AssertSuitePassed(Run(WriterReaderCorrectnessRegressionTest.Run));

        [Fact]
        public void BufferResizeColdPathRegression()
            => AssertSuitePassed(Run(BufferResizeColdPathRegressionTest.Run));

        [Fact]
        public void StringSerializerFallback()
            => AssertSuitePassed(Run(StringSerializerFallbackTest.Run));

        [Fact]
        public void InterfaceCollectionReadRegression()
            => AssertSuitePassed(Run(InterfaceCollectionReadRegressionTest.Run));

        [Fact]
        public void PruneGenericWrapper()
            => AssertSuitePassed(Run(PruneGenericWrapperTest.Run));
    }
}
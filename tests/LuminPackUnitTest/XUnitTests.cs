using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>
    /// xUnit runner over the same shared test methods the console <c>Program.Main</c> uses.
    /// Console mode runs the full suite (dotnet run); xUnit mode (dotnet test) runs the same
    /// suite one test case per logical group, asserting every collected result passed.
    /// </summary>
    [Collection(nameof(RuntimeStateCollection))]
    public class LuminPackXUnitTests
    {
        /// <summary>Verifies run.</summary>
        /// <param name="runner">The runner used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        private static List<string> Run(Action<List<string>> runner)
        {
            var results = new List<string>();
            runner(results);
            return results;
        }

        /// <summary>Verifies assert suite passed.</summary>
        /// <param name="results">The results used by this operation.</param>
        private static void AssertSuitePassed(List<string> results)
        {
            // "?" marks a case that cannot run in the current configuration - currently the reader
            // boundary checks, whose EnsureReadable call is compiled out in Release. That is a
            // deliberate skip, so it must not be reported as a failure.
            var failures = results
                .Where(static result => !result.StartsWith("✓") && !result.StartsWith("?"))
                .ToArray();
            Assert.True(failures.Length == 0,
                "Suite failures (" + failures.Length + "):\n" + string.Join("\n", failures));
        }

        /// <summary>Verifies core serialization suite.</summary>
        [Fact]
        public void CoreSerializationSuite()
            => AssertSuitePassed(Run(Program.RunCoreTests));

        /// <summary>Verifies buffer writer serialization.</summary>
        [Fact]
        public void BufferWriterSerialization()
            => AssertSuitePassed(Run(BufferWriterSerializationTest.Run));

        /// <summary>Verifies json writer safety.</summary>
        [Fact]
        public void JsonWriterSafety()
            => AssertSuitePassed(Run(JsonWriterSafetyTest.Run));

        /// <summary>Verifies json reader safety.</summary>
        [Fact]
        public void JsonReaderSafety()
            => AssertSuitePassed(Run(JsonReaderSafetyTest.Run));

        /// <summary>Compresses the input using the LuminPack compression format.</summary>
        [Fact]
        public void CompressionSafetyPerf()
            => AssertSuitePassed(Run(CompressionSafetyPerfTest.Run));

        /// <summary>Reads er boundary validation from the current input.</summary>
        [Fact]
        public void ReaderBoundaryValidation()
            => AssertSuitePassed(Run(ReaderBoundaryValidationTest.Run));

        /// <summary>Verifies queue stack regression.</summary>
        [Fact]
        public void QueueStackRegression()
            => AssertSuitePassed(Run(QueueStackRegressionTest.Run));

        /// <summary>Verifies async serializer regression.</summary>
        [Fact]
        public void AsyncSerializerRegression()
            => AssertSuitePassed(Run(AsyncSerializerRegressionTest.Run));

        /// <summary>Serializes the supplied value using the selected format and options.</summary>
        [Fact]
        public void SerializerReentrancy()
            => AssertSuitePassed(Run(SerializerReentrancyTest.Run));

        /// <summary>Verifies binary union safety.</summary>
        [Fact]
        public void BinaryUnionSafety()
            => AssertSuitePassed(Run(BinaryUnionSafetyTest.Run));

        /// <summary>Verifies binary direct result regression.</summary>
        [Fact]
        public void BinaryDirectResultRegression()
            => AssertSuitePassed(Run(BinaryDirectResultRegressionTest.Run));

        /// <summary>Verifies confirmed correctness regression.</summary>
        [Fact]
        public void ConfirmedCorrectnessRegression()
            => AssertSuitePassed(Run(ConfirmedCorrectnessRegressionTest.Run));

        /// <summary>Verifies object pool regression.</summary>
        [Fact]
        public void ObjectPoolRegression()
            => AssertSuitePassed(Run(ObjectPoolRegressionTest.Run));

        /// <summary>Verifies lumin buffer writer pool regression.</summary>
        [Fact]
        public void LuminBufferWriterPoolRegression()
            => AssertSuitePassed(Run(LuminBufferWriterPoolRegressionTest.Run));

        /// <summary>Verifies deep private member.</summary>
        [Fact]
        public void DeepPrivateMember()
            => AssertSuitePassed(Run(DeepPrivateMemberTest.Run));

        /// <summary>Verifies shallow private member.</summary>
        [Fact]
        public void ShallowPrivateMember()
            => AssertSuitePassed(Run(ShallowPrivateMemberTest.Run));

        /// <summary>Verifies local layout invariant.</summary>
        [Fact]
        public void LocalLayoutInvariant()
            => AssertSuitePassed(Run(LocalLayoutInvariantTest.Run));

        /// <summary>Verifies layout kind private.</summary>
        [Fact]
        public void LayoutKindPrivate()
            => AssertSuitePassed(Run(LayoutKindPrivateTest.Run));

        /// <summary>Verifies on demand local layout.</summary>
        [Fact]
        public void OnDemandLocalLayout()
            => AssertSuitePassed(Run(OnDemandLocalLayoutTest.Run));

        /// <summary>Verifies buffer writer operation context.</summary>
        [Fact]
        public void BufferWriterOperationContext()
            => AssertSuitePassed(Run(BufferWriterOperationContextTest.Run));

        /// <summary>Writes r reader correctness regression to the current output.</summary>
        [Fact]
        public void WriterReaderCorrectnessRegression()
            => AssertSuitePassed(Run(WriterReaderCorrectnessRegressionTest.Run));

        /// <summary>Verifies buffer resize cold path regression.</summary>
        [Fact]
        public void BufferResizeColdPathRegression()
            => AssertSuitePassed(Run(BufferResizeColdPathRegressionTest.Run));

        /// <summary>Verifies string serializer fallback.</summary>
        [Fact]
        public void StringSerializerFallback()
            => AssertSuitePassed(Run(StringSerializerFallbackTest.Run));

        /// <summary>Verifies interface collection read regression.</summary>
        [Fact]
        public void InterfaceCollectionReadRegression()
            => AssertSuitePassed(Run(InterfaceCollectionReadRegressionTest.Run));

        /// <summary>Verifies prune generic wrapper.</summary>
        [Fact]
        public void PruneGenericWrapper()
            => AssertSuitePassed(Run(PruneGenericWrapperTest.Run));

        /// <summary>Verifies model boundary.</summary>
        [Fact]
        public void ModelBoundary()
            => AssertSuitePassed(Run(ModelBoundaryTest.Run));

        /// <summary>Verifies i buffer writer interop.</summary>
        [Fact]
        public void IBufferWriterInterop()
            => AssertSuitePassed(Run(IBufferWriterInteropTest.Run));

        /// <summary>Verifies manual registration fallback.</summary>
        [Fact]
        public void ManualRegistrationFallback()
            => AssertSuitePassed(Run(ManualRegistrationTest.Run));

#if NET11_0_OR_GREATER
        /// <summary>Verifies net11 union.</summary>
        [Fact]
        public void Net11Union()
            => AssertSuitePassed(Run(Net11UnionTest.Run));
#endif
    }
}

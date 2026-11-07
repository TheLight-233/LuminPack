using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LuminPack.Build;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Unity.LuminPack.CodeGen
{
    /// <summary>Applies LuminPack static entry mappings before Mono loading or IL2CPP conversion.</summary>
    public sealed class LuminPackILPostProcessor : ILPostProcessor
    {
        /// <summary>Creates an independent processor for the current compilation.</summary>
        /// <returns>A processor without shared mutable compilation state.</returns>
        public override ILPostProcessor GetInstance() => new LuminPackILPostProcessor();

        /// <summary>Filters assemblies before reading their metadata.</summary>
        /// <param name="compiledAssembly">The Unity compilation result.</param>
        /// <returns>Whether it references Core and may contain generated mappings.</returns>
        public override bool WillProcess(ICompiledAssembly compiledAssembly)
            => compiledAssembly.References.Any(path => Path.GetFileNameWithoutExtension(path) == "LuminPack.Core");

        /// <summary>Rewrites the in-memory images while preserving portable sequence points.</summary>
        /// <param name="compiledAssembly">The assembly before downstream Unity processing.</param>
        /// <returns>The replacement images and diagnostics, or null when no work is needed.</returns>
        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            if (!WillProcess(compiledAssembly))
                return null;
            try
            {
                var result = StaticCallAssembly.Rewrite(compiledAssembly.InMemoryAssembly.PeData,
                    compiledAssembly.InMemoryAssembly.PdbData, compiledAssembly.References);
                return result == null ? null : new ILPostProcessResult(new InMemoryAssembly(result.Pe, result.Pdb), new List<DiagnosticMessage>());
            }
            catch (Exception error)
            {
                return new ILPostProcessResult(compiledAssembly.InMemoryAssembly, new List<DiagnosticMessage>
                {
                    new DiagnosticMessage { DiagnosticType = DiagnosticType.Error, MessageData = "LuminPack IL rewriting failed: " + error }
                });
            }
        }
    }
}

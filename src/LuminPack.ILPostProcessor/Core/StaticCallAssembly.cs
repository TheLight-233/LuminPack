using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LuminPack.Build
{
    /// <summary>Preserves portable symbols while applying static call mappings to a compiled image.</summary>
    public static class StaticCallAssembly
    {
        /// <summary>Rewrites an assembly before it is loaded, copied or passed to an AOT compiler.</summary>
        /// <param name="pe">The original assembly bytes.</param>
        /// <param name="pdb">The portable PDB bytes, or an empty array.</param>
        /// <param name="references">The compiler's resolved reference paths.</param>
        /// <param name="signingKey">The original strong-name key, or null for unsigned assemblies.</param>
        /// <returns>The rewritten images, or null when no mapping exists.</returns>
        public static Result Rewrite(byte[] pe, byte[] pdb, string[] references, byte[] signingKey = null)
        {
            using var resolver = new DefaultAssemblyResolver();
            foreach (string directory in references.Select(Path.GetDirectoryName).Distinct())
                resolver.AddSearchDirectory(directory);
            using var input = new MemoryStream(pe, false);
            using var symbols = new MemoryStream(pdb ?? Array.Empty<byte>(), false);
            var reader = new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Immediate };
            if (symbols.Length != 0)
            {
                reader.ReadSymbols = true;
                reader.SymbolReaderProvider = new PortablePdbReaderProvider();
                reader.SymbolStream = symbols;
            }
            using var module = ModuleDefinition.ReadModule(input, reader);
            if (!module.GetTypes().SelectMany(type => type.Methods).Any(method => method.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName == "LuminPack.Generated.StaticCallAttribute")))
                return null;
            if (module.Assembly.Name.HasPublicKey && (signingKey == null || signingKey.Length == 0))
                LuminPackExceptionHelper.ThrowInvalidOperationException("Rewriting requires the original strong-name key for signed assemblies. Supply AssemblyOriginatorKeyFile or disable LuminPackUseIlRewriting.");
            int calls = StaticCallRewriter.Rewrite(module);
            using var output = new MemoryStream();
            using var outputSymbols = new MemoryStream();
            var writer = new WriterParameters { StrongNameKeyBlob = signingKey };
            if (reader.ReadSymbols)
            {
                writer.WriteSymbols = true;
                writer.SymbolWriterProvider = new PortablePdbWriterProvider();
                writer.SymbolStream = outputSymbols;
            }
            module.Write(output, writer);
            return new Result(output.ToArray(), outputSymbols.ToArray(), calls);
        }

        /// <summary>The complete replacement images and rewrite count.</summary>
        public sealed class Result
        {
            /// <summary>Gets the rewritten assembly.</summary>
            public byte[] Pe { get; }
            /// <summary>Gets the rewritten portable symbols, when present.</summary>
            public byte[] Pdb { get; }
            /// <summary>Gets the number of direct calls substituted.</summary>
            public int Calls { get; }

            /// <summary>Captures images only after every mapping has been validated.</summary>
            /// <param name="pe">The replacement assembly.</param>
            /// <param name="pdb">The replacement portable symbols.</param>
            /// <param name="calls">The rewritten call count.</param>
            internal Result(byte[] pe, byte[] pdb, int calls)
            {
                Pe = pe;
                Pdb = pdb;
                Calls = calls;
            }
        }
    }
}

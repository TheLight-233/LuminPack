using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace LuminPack.Build
{
    /// <summary>Applies static entry substitutions to the intermediate consumer assembly.</summary>
    public sealed class RewriteTask : Task
    {
        /// <summary>Gets or sets the compiler output before copying it to the destination.</summary>
        [Required]
        public string AssemblyPath { get; set; }
        /// <summary>Gets or sets the compiler reference closure.</summary>
        public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();
        /// <summary>Gets or sets the original strong-name signing key path.</summary>
        public string KeyFile { get; set; }

        /// <summary>Validates, rewrites and publishes the intermediate images.</summary>
        /// <returns>Whether rewriting completed without a build error.</returns>
        public override bool Execute()
        {
            try
            {
                string pdbPath = Path.ChangeExtension(AssemblyPath, ".pdb");
                var result = StaticCallAssembly.Rewrite(File.ReadAllBytes(AssemblyPath),
                    File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : Array.Empty<byte>(),
                    References.Select(item => item.ItemSpec).ToArray(),
                    string.IsNullOrEmpty(KeyFile) ? null : File.ReadAllBytes(KeyFile));
                if (result == null)
                    return true;
                File.WriteAllBytes(AssemblyPath, result.Pe);
                if (result.Pdb.Length != 0)
                    File.WriteAllBytes(pdbPath, result.Pdb);
                Log.LogMessage(MessageImportance.Normal, "LuminPack replaced {0} static calls in {1}.", result.Calls, AssemblyPath);
                return true;
            }
            catch (Exception error)
            {
                Log.LogErrorFromException(error, showStackTrace: true);
                return false;
            }
        }
    }
}

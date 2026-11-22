/// <summary>Publishes a validated tool output atomically in the requested directory.</summary>
internal static class OutputFile
{
    /// <summary>Writes a sibling temporary file and replaces the output after a successful write.</summary>
    /// <param name="inputPath">The input file to protect from overwriting.</param>
    /// <param name="outputPath">The requested output file.</param>
    /// <param name="write">The serializer that writes into the temporary stream.</param>
    internal static void Replace(string inputPath, string outputPath, Action<Stream> write)
    {
        string output = Path.GetFullPath(outputPath);
        if (string.Equals(Path.GetFullPath(inputPath), output,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("The output path must differ from the input path.");

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string pending = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = File.Open(pending, FileMode.CreateNew))
                write(stream);
            File.Move(pending, output, overwrite: true);
        }
        finally
        {
            File.Delete(pending);
        }
    }
}

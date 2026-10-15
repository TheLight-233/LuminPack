using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses the unchanged legacy aggregate without concatenating its large type blocks again.</summary>
internal sealed class SerializerFacadeCache
{
    internal readonly TypeEmissionCache Fragments = new TypeEmissionCache();
    internal readonly PartitionedSourceCache Files = new PartitionedSourceCache();
    internal readonly BinaryOperationPlanCache BinaryOperations = new BinaryOperationPlanCache();
    private readonly object _gate = new object();
    private string _header;
    private string _tail;
    private string[] _blocks;
    private string _result;

    /// <summary>Assembles the legacy facade only when its component texts change.</summary>
    /// <param name="header">The unchanged legacy header.</param>
    /// <param name="blocks">Concrete overload blocks in legacy order.</param>
    /// <param name="tail">Generic methods, helpers, and the closing wrapper.</param>
    /// <returns>The complete facade, preserving the previous string when possible.</returns>
    internal string Assemble(string header, IReadOnlyList<string> blocks, string tail)
    {
        lock (_gate)
        {
            if (_result != null && _header == header && _tail == tail && _blocks.SequenceEqual(blocks)) return _result;
            var builder = new StringBuilder(header.Length + tail.Length + blocks.Sum(block => block.Length));
            builder.Append(header);
            foreach (var block in blocks) builder.Append(block);
            builder.Append(tail);
            _header = header;
            _tail = tail;
            _blocks = blocks.ToArray();
            return _result = builder.ToString();
        }
    }
}

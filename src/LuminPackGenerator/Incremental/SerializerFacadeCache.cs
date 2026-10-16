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
    private readonly object _gate = new object();
    private string _header;
    private string _tail;
    private string[] _blocks;
    private string _result;
    private MetaInfo _commonSettings;
    private string _common;

    /// <summary>Reuses common generic APIs and their XML independently of concrete payload edits.</summary>
    /// <param name="metadata">All parse and generation settings used by the common emitters.</param>
    /// <param name="build">Renders and documents the common contribution when settings change.</param>
    /// <returns>The exact common source without retaining the compilation used to build it.</returns>
    internal string GetCommon(MetaInfo metadata, Func<string> build)
    {
        lock (_gate)
        {
            if (_common != null && MetaInfoValueComparer.Instance.Equals(_commonSettings, metadata)) return _common;
            string source = build();
            _commonSettings = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8,
                metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            return _common = source;
        }
    }

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

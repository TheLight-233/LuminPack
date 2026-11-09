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
    internal readonly SharedFacadeProjection Sharing = new SharedFacadeProjection();
    private readonly object _gate = new object();

    private MetaInfo _commonSettings;
    private string _commonProfile;
    private string _common;

    /// <summary>Reuses common generic APIs and their XML independently of concrete payload edits.</summary>
    /// <param name="metadata">All parse and generation settings used by the common emitters.</param>
    /// <param name="profile">The consuming assembly's complete compiled option identity.</param>
    /// <param name="build">Renders and documents the common contribution when settings change.</param>
    /// <returns>The exact common source without retaining the compilation used to build it.</returns>
    internal string GetCommon(MetaInfo metadata, string profile, Func<string> build)
    {
        lock (_gate)
        {
            if (_common != null && _commonProfile == profile && MetaInfoValueComparer.Instance.Equals(_commonSettings, metadata))
                return _common;
            string source = build();
            _commonSettings = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8,
                metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            _commonProfile = profile;
            return _common = source;
        }
    }

}

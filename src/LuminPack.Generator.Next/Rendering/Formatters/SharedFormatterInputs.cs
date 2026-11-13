using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Retains one driver's exact formatter dependencies independently of interception locations.</summary>
/// <remarks>Only immutable value models and options survive publication. A changed model, root order or project option
/// invalidates the set; operation selection is applied separately to its generated owners.</remarks>
internal sealed class SharedFormatterInputs
{
    private readonly TypeModel[] _models;
    private readonly MemberModel[] _roots;
    private readonly GeneratorOptions _options;

    internal readonly SharedFormatterSet Set;
    private HashSet<string>? _publishedRoots;

    /// <summary>Shares the read-only root index between registration and interception.</summary>
    internal HashSet<string> PublishedRoots => _publishedRoots ??= Set.PublishedRoots();

    /// <summary>Freezes the dependency lists and collects their shared owners once.</summary>
    /// <param name="models">The retained payloads in publication order.</param>
    /// <param name="roots">The non-packable roots in discovery order.</param>
    /// <param name="options">Every project fact used by collection and rendering.</param>
    internal SharedFormatterInputs(List<TypeModel> models, List<MemberModel> roots, GeneratorOptions options)
    {
        _models = models.ToArray();
        _roots = roots.ToArray();
        _options = options;
        Set = models.Count == 0 && roots.Count == 0 ? SharedFormatterSet.Empty :
            SharedFormatterSet.Collect(_models, options, roots: _roots);
    }

    /// <summary>Checks semantic dependencies without rebuilding render models, sorting owners or copying lists.</summary>
    /// <param name="models">The current retained payloads.</param>
    /// <param name="roots">The current non-packable roots.</param>
    /// <param name="options">The current project facts.</param>
    /// <returns>Whether the existing set can be reused exactly.</returns>
    internal bool Matches(List<TypeModel> models, List<MemberModel> roots, GeneratorOptions options)
    {
        if (!_options.Equals(options) || _models.Length != models.Count || _roots.Length != roots.Count)
            return false;
        for (int index = 0; index < _models.Length; index++)
            if (!_models[index].Equals(models[index]))
                return false;
        for (int index = 0; index < _roots.Length; index++)
            if (!_roots[index].Equals(roots[index]))
                return false;
        return true;
    }
}

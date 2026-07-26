using Exp01_StaticRender.Protocol;

namespace Exp01_StaticRender.Surfaces;

/// <summary>
/// A component with its children already looked up by id — the output of
/// resolving the flat adjacency list into a tree, and the input to the renderer.
/// </summary>
internal sealed record ResolvedNode(ComponentNode Component, IReadOnlyList<ResolvedNode> Children);

/// <summary>
/// One A2UI surface: the adjacency list of its components keyed by id, plus the
/// walk that rebuilds a tree from it.
/// </summary>
internal sealed class Surface(string surfaceId, string? catalogId)
{
    /// <summary>A2UI names the entry point of every surface's tree "root".</summary>
    public const string RootId = "root";

    private readonly Dictionary<string, ComponentNode> _components = new(StringComparer.Ordinal);

    public string SurfaceId { get; } = surfaceId;

    public string? CatalogId { get; } = catalogId;

    public int ComponentCount => _components.Count;

    /// <summary>
    /// Adds the components, replacing any existing entry with the same id. The
    /// wire format is add-or-replace by id, which is what makes the incremental
    /// updates of experiment 03 possible without changing this method.
    /// </summary>
    public void Apply(IEnumerable<ComponentNode> components)
    {
        foreach (var component in components)
        {
            _components[component.Id] = component;
        }
    }

    /// <summary>
    /// Rebuilds the tree from <see cref="RootId"/>, resolving each child id
    /// against the adjacency list.
    /// </summary>
    public ResolvedNode Resolve() => Resolve(RootId);

    private ResolvedNode Resolve(string id)
    {
        if (!_components.TryGetValue(id, out var component))
        {
            throw new InvalidOperationException(
                $"Component '{id}' is referenced as a child of surface '{SurfaceId}' but was never defined.");
        }

        // Note: no cycle guard. The input here is a fixed fixture, and a real
        // producer only arrives in experiment 04 — see the open questions.
        var children = component.Children.Count == 0
            ? []
            : component.Children.Select(Resolve).ToArray();

        return new ResolvedNode(component, children);
    }
}

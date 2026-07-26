using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exp01_StaticRender.Protocol;

/// <summary>
/// One line of the A2UI stream: a version plus exactly one message payload.
/// Only the three messages experiment 01 needs are modelled; anything else on
/// the line deserializes to null and is ignored.
/// </summary>
internal sealed record A2uiMessage
{
    public string? Version { get; init; }

    public CreateSurface? CreateSurface { get; init; }

    public UpdateComponents? UpdateComponents { get; init; }

    public BeginRendering? BeginRendering { get; init; }
}

/// <summary>Opens a surface and names the catalog its components come from.</summary>
internal sealed record CreateSurface
{
    public string SurfaceId { get; init; } = "";

    public string? CatalogId { get; init; }
}

/// <summary>Adds or replaces components in a surface's adjacency list.</summary>
internal sealed record UpdateComponents
{
    public string SurfaceId { get; init; } = "";

    public IReadOnlyList<ComponentNode> Components { get; init; } = [];
}

/// <summary>Tells the host the surface is complete and can be rendered.</summary>
internal sealed record BeginRendering
{
    public string SurfaceId { get; init; } = "";
}

/// <summary>
/// A single entry of the flat adjacency list. Children are referenced by id, not
/// nested, so the tree is reconstructed by the SurfaceManager rather than by the
/// deserializer.
/// </summary>
internal sealed record ComponentNode
{
    public string Id { get; init; } = "";

    /// <summary>The catalog component type, e.g. <c>Column</c> or <c>TextField</c>.</summary>
    public string Component { get; init; } = "";

    public IReadOnlyList<string> Children { get; init; } = [];

    /// <summary>
    /// Every other property on the node — <c>text</c>, <c>label</c>, … — sits
    /// inline as a sibling of <c>component</c> in the wire format. Capturing them
    /// generically keeps the parser open-ended: which properties matter is the
    /// catalog's decision, so adding a component type later needs no parser change.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Properties { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>
    /// Reads a literal string property. Returns null when absent or not a string —
    /// binding expressions (<c>{path}</c>) are out of scope for experiment 01.
    /// </summary>
    public string? GetString(string name) =>
        Properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

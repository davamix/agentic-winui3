using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exp02_BindingAndActions.Protocol;

/// <summary>
/// One line of the A2UI stream: a version plus exactly one message payload.
/// Only the messages this experiment needs are modelled; anything else on the
/// line deserializes to null and is ignored.
/// </summary>
internal sealed record A2uiMessage
{
    public string? Version { get; init; }

    public CreateSurface? CreateSurface { get; init; }

    public UpdateComponents? UpdateComponents { get; init; }

    /// <summary>New in experiment 02 — see <see cref="Protocol.UpdateDataModel"/>.</summary>
    public UpdateDataModel? UpdateDataModel { get; init; }

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

/// <summary>
/// Writes <see cref="Value"/> into the surface's data model at <see cref="Path"/>.
/// Per the spec the path is optional and defaults to the model root, and an
/// omitted value removes the key — this experiment's fixtures use both a root
/// write (seeding <c>/form</c>) and a leaf write (the response's
/// <c>/form/status</c>), but never the removal case.
/// </summary>
internal sealed record UpdateDataModel
{
    public string SurfaceId { get; init; } = "";

    public string Path { get; init; } = "/";

    public JsonElement? Value { get; init; }
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
    /// Every other property on the node — <c>text</c>, <c>label</c>, <c>value</c>,
    /// <c>action</c>, … — sits inline as a sibling of <c>component</c> in the wire
    /// format. Capturing them generically keeps the parser open-ended: which
    /// properties matter is the catalog's decision, so adding a component type
    /// later needs no parser change.
    ///
    /// Experiment 02 is the first real test of that claim: binding and actions
    /// arrive as new *readers* over this same dictionary
    /// (<see cref="GetDynamic"/>, <see cref="GetAction"/>) rather than as new
    /// fields, so nothing about how a node is parsed had to change.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Properties { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>
    /// Reads a property that must be a literal string — used for the ones this
    /// catalog does not allow to be bound (<c>label</c>, <c>Button.text</c>).
    /// Returns null when absent or not a string.
    /// </summary>
    public string? GetString(string name) =>
        Properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Reads a property that may be either a literal or a <c>{"path": …}</c>
    /// binding. Returns null when the property is absent.
    /// </summary>
    public DynamicValue? GetDynamic(string name) =>
        Properties.TryGetValue(name, out var value) ? DynamicValue.From(value) : null;

    /// <summary>Reads this component's <c>action</c> declaration, or null when it has none.</summary>
    public ActionDeclaration? GetAction() =>
        Properties.TryGetValue("action", out var value) ? ActionDeclaration.From(value) : null;
}

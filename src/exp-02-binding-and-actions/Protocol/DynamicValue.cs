using System.Text.Json;

namespace Exp02_BindingAndActions.Protocol;

/// <summary>
/// A2UI's <c>DynamicString</c>: a property value that is either written straight
/// into the message or read from the data model. The spec allows a third form —
/// a function call — which is out of scope for this experiment.
/// </summary>
internal abstract record DynamicValue
{
    // Private constructor closes the hierarchy: Literal and Bound are the only
    // two cases, so a `switch` over them can be exhaustive.
    private DynamicValue()
    {
    }

    /// <summary>A value carried by the message itself.</summary>
    public sealed record Literal(JsonElement Value) : DynamicValue;

    /// <summary>A JSON Pointer into the surface's data model, e.g. <c>/form/email</c>.</summary>
    public sealed record Bound(string Path) : DynamicValue;

    /// <summary>
    /// A JSON object carrying a string <c>path</c> is a binding; anything else is
    /// a literal. The distinction is *structural*, not syntactic — which is why a
    /// literal string may contain braces without ever being mistaken for a
    /// binding (the response fixture's <c>${…}</c> placeholders rely on this).
    /// </summary>
    public static DynamicValue From(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("path", out var path)
        && path.ValueKind == JsonValueKind.String
            ? new Bound(path.GetString()!)
            : new Literal(element);
}

/// <summary>
/// The <c>action</c> a component declares: what the event is called, and which
/// values to gather from the data model when it fires. Wire shape:
/// <c>{"action": {"event": {"name": "submit", "context": {"email": {"path": "/form/email"}}}}}</c>.
/// </summary>
/// <param name="Name">The event name the producer will receive.</param>
/// <param name="Context">
/// Values to send with the event. Each is resolved at *click* time, not at render
/// time, which is what makes the action carry what the user actually typed.
/// </param>
internal sealed record ActionDeclaration(
    string Name,
    IReadOnlyDictionary<string, DynamicValue> Context)
{
    /// <summary>
    /// Parses the declaration, or returns null if the shape is not recognised —
    /// a component whose action cannot be understood renders inert rather than
    /// failing the whole surface, since the control itself is still valid.
    /// </summary>
    public static ActionDeclaration? From(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("event", out var declaredEvent)
            || declaredEvent.ValueKind != JsonValueKind.Object
            || !declaredEvent.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var context = new Dictionary<string, DynamicValue>(StringComparer.Ordinal);
        if (declaredEvent.TryGetProperty("context", out var declaredContext)
            && declaredContext.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in declaredContext.EnumerateObject())
            {
                context[entry.Name] = DynamicValue.From(entry.Value);
            }
        }

        return new ActionDeclaration(name.GetString()!, context);
    }
}

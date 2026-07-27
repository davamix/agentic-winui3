using System.Text.Json;
using Exp02_BindingAndActions.Protocol;
using Exp02_BindingAndActions.Surfaces;

namespace Exp02_BindingAndActions.Rendering;

/// <summary>
/// Connects a component's <see cref="DynamicValue"/> properties to the surface's
/// data model.
///
/// Note what this class does *not* mention: any WinUI type. It hands values to an
/// <see cref="Action{T}"/> the caller supplies, so the catalog decides which
/// control property a binding lands on. That keeps the "what does this path mean"
/// question separate from the "which control shows it" question — and, as a side
/// effect, leaves this class testable without a UI thread.
///
/// WinUI's own binding was not used on purpose: <c>{x:Bind}</c> is compile-time,
/// and <c>{Binding}</c> needs a source object with real properties, which a
/// JSON-Pointer-addressed model does not have. See research.md §8, hard problem 4.
/// </summary>
internal sealed class BindingResolver(DataModel model)
{
    /// <summary>
    /// Applies a value now, and re-applies it whenever the model changes beneath
    /// it. A literal is applied once and never subscribes; an absent property
    /// yields empty. This is the one-way half.
    /// </summary>
    public void Bind(DynamicValue? value, Action<string> apply)
    {
        switch (value)
        {
            case null:
                apply(string.Empty);
                break;

            case DynamicValue.Literal literal:
                apply(AsText(literal.Value));
                break;

            case DynamicValue.Bound bound:
                apply(model.ReadString(bound.Path));

                // Never unsubscribed. Safe only because this experiment builds
                // the tree once and the controls live as long as the surface —
                // a re-render would leave these handlers alive, holding dead
                // controls. See the experiment's open questions.
                model.Changed += changed =>
                {
                    if (DataModel.Affects(changed, bound.Path))
                    {
                        apply(model.ReadString(bound.Path));
                    }
                };
                break;
        }
    }

    /// <summary>
    /// The write-back half, for properties the catalog binds two-way. Returns
    /// null for a literal — a component bound to nothing has nowhere to write,
    /// and the catalog uses that null to decide not to subscribe to edits at all.
    /// </summary>
    public Action<string>? WriteBack(DynamicValue? value) =>
        value is DynamicValue.Bound bound
            ? text => model.WriteString(bound.Path, text)
            : null;

    /// <summary>
    /// Resolves an action's declared context to concrete values — done at click
    /// time, so what travels is what the user actually typed.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> ResolveContext(
        IReadOnlyDictionary<string, DynamicValue> context)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var (key, value) in context)
        {
            resolved[key] = value switch
            {
                DynamicValue.Bound bound => model.ReadElement(bound.Path),
                DynamicValue.Literal literal => literal.Value,
                _ => default,
            };
        }

        return resolved;
    }

    private static string AsText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}

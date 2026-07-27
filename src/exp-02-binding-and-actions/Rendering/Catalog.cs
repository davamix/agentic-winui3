using Exp02_BindingAndActions.Actions;
using Exp02_BindingAndActions.Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exp02_BindingAndActions.Rendering;

/// <summary>
/// What a factory needs besides the component itself. Passing one object rather
/// than a growing parameter list means adding a capability later (a theme, a
/// navigation callback) does not touch every factory signature.
/// </summary>
internal sealed record RenderContext(BindingResolver Bindings, ActionChannel Actions);

/// <summary>
/// Builds the native control for one component. Children arrive already built,
/// so a factory never recurses — the renderer owns the walk.
/// </summary>
internal delegate FrameworkElement ComponentFactory(
    ComponentNode component,
    IReadOnlyList<FrameworkElement> children,
    RenderContext context);

/// <summary>
/// The component catalog: the closed set of A2UI component types this host can
/// render, and the native control each one becomes. Because the mapping lives
/// here and nowhere else, the agent can only ever name a control the host already
/// knows how to build — that is the whole safety story of the approach, and the
/// reason no code from the stream is ever executed.
///
/// Experiment 02 changes no *membership* — still the same four types — but three
/// of them gain behaviour. That the set stayed the same while the surface became
/// interactive is itself a result: binding and actions are properties of the
/// catalog's *mapping*, not of its vocabulary.
/// </summary>
internal static class Catalog
{
    /// <summary>
    /// Informal and local to this experiment. There is no JSON-Schema validation
    /// yet; formalising the catalog as a published allow-list is a later concern.
    /// </summary>
    public const string CatalogId = "local/winui-basic/v0";

    private static readonly Dictionary<string, ComponentFactory> Factories =
        new(StringComparer.Ordinal)
        {
            ["Column"] = (_, children, _) =>
            {
                var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
                foreach (var child in children)
                {
                    panel.Children.Add(child);
                }

                return panel;
            },

            // One-way. `text` may be a literal or a {path}; the resolver applies
            // whichever it is now, and re-applies on every later model change.
            ["Text"] = (component, _, context) =>
            {
                var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
                context.Bindings.Bind(component.GetDynamic("text"), text => block.Text = text);
                return block;
            },

            // Two-way. `label` stays a literal — it names the field, it is not
            // state — while `value` binds in both directions.
            ["TextField"] = (component, _, context) =>
            {
                var box = new TextBox { Header = component.GetString("label") };
                var value = component.GetDynamic("value");

                context.Bindings.Bind(value, text =>
                {
                    // Guard the echo: this control's own edit writes to the model,
                    // which notifies straight back here. Re-assigning the identical
                    // string would be a no-op for the dependency property, but the
                    // guard makes the intent explicit and keeps a future formatting
                    // binding from fighting the user's caret.
                    if (box.Text != text)
                    {
                        box.Text = text;
                    }
                });

                if (context.Bindings.WriteBack(value) is { } writeBack)
                {
                    box.TextChanged += (_, _) => writeBack(box.Text);
                }

                return box;
            },

            // The only component that sends anything back. A Button without an
            // action declaration renders inert, exactly as in experiment 01 —
            // interactivity is something the producer asks for, not a default.
            ["Button"] = (component, _, context) =>
            {
                var button = new Button
                {
                    Content = component.GetString("text") ?? string.Empty,
                };

                if (component.GetAction() is { } action)
                {
                    button.Click += (_, _) => context.Actions.Send(action, component.Id);
                }

                return button;
            },
        };

    /// <summary>
    /// Looks up the factory for a component type. Throws for anything unknown —
    /// graceful degradation of unrecognised components is out of scope here, and
    /// failing loudly keeps the catalog's boundary visible.
    /// </summary>
    public static ComponentFactory Get(string componentType) =>
        Factories.TryGetValue(componentType, out var factory)
            ? factory
            : throw new NotSupportedException(
                $"Component type '{componentType}' is not in catalog '{CatalogId}'.");
}

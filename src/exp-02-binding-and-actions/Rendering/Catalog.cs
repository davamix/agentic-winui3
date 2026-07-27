using Exp02_BindingAndActions.Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Exp02_BindingAndActions.Rendering;

/// <summary>
/// Builds the native control for one component. Children arrive already built,
/// so a factory never recurses — the renderer owns the walk.
/// </summary>
internal delegate FrameworkElement ComponentFactory(
    ComponentNode component,
    IReadOnlyList<FrameworkElement> children);

/// <summary>
/// The component catalog: the closed set of A2UI component types this host can
/// render, and the native control each one becomes. Because the mapping lives
/// here and nowhere else, the agent can only ever name a control the host already
/// knows how to build — that is the whole safety story of the approach, and the
/// reason no code from the stream is ever executed.
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
            ["Column"] = (_, children) =>
            {
                var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };
                foreach (var child in children)
                {
                    panel.Children.Add(child);
                }

                return panel;
            },

            ["Text"] = (component, _) => new TextBlock
            {
                Text = component.GetString("text") ?? string.Empty,
            },

            ["TextField"] = (component, _) => new TextBox
            {
                Header = component.GetString("label"),
            },

            // No Click handler: actions are experiment 02. The button renders inert.
            ["Button"] = (component, _) => new Button
            {
                Content = component.GetString("text") ?? string.Empty,
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

using Exp02_BindingAndActions.Surfaces;
using Microsoft.UI.Xaml;

namespace Exp02_BindingAndActions.Rendering;

/// <summary>
/// Turns a resolved component tree into a native WinUI control tree.
/// Build-once: there is no diffing against a previous render, which is what
/// experiment 03 adds.
/// </summary>
internal static class Renderer
{
    /// <summary>
    /// Builds depth-first — children first, then the parent factory receives them
    /// already constructed. Must be called on the UI thread, since it creates
    /// XAML controls.
    /// </summary>
    public static FrameworkElement Build(ResolvedNode node)
    {
        var children = node.Children.Count == 0
            ? []
            : node.Children.Select(Build).ToArray();

        var factory = Catalog.Get(node.Component.Component);
        return factory(node.Component, children);
    }
}

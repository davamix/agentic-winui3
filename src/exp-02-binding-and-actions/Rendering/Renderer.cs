using Exp02_BindingAndActions.Surfaces;
using Microsoft.UI.Xaml;

namespace Exp02_BindingAndActions.Rendering;

/// <summary>
/// Turns a resolved component tree into a native WinUI control tree.
///
/// Still build-once, and deliberately so. Experiment 02 makes the surface *live*
/// without making it rebuild: state changes reach the controls through the
/// bindings the factories set up during this single pass, never through a second
/// call to <see cref="Build"/>. Diffing an existing tree against a new one is
/// experiment 03's problem.
/// </summary>
internal static class Renderer
{
    /// <summary>
    /// Builds depth-first — children first, then the parent factory receives them
    /// already constructed. Must be called on the UI thread, since it creates
    /// XAML controls.
    /// </summary>
    public static FrameworkElement Build(ResolvedNode node, RenderContext context)
    {
        var children = node.Children.Count == 0
            ? []
            : node.Children.Select(child => Build(child, context)).ToArray();

        var factory = Catalog.Get(node.Component.Component);
        return factory(node.Component, children, context);
    }
}

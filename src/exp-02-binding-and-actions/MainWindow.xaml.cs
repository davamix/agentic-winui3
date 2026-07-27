using Exp02_BindingAndActions.Protocol;
using Exp02_BindingAndActions.Rendering;
using Exp02_BindingAndActions.Surfaces;
using Microsoft.UI.Xaml;

namespace Exp02_BindingAndActions;

/// <summary>
/// The application window: a surface host on the left where the A2UI component
/// tree is rendered as native controls, and a log pane on the right listing the
/// messages read from the stream.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The fixture linked into the project by the csproj, so it sits next to the
    /// app (and inside the MSIX package) rather than at a path relative to the repo.
    /// </summary>
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "Samples", "contact-form-bound.jsonl");

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
    }

    /// <summary>
    /// Reads the whole stream and renders it once. Loaded already runs on the UI
    /// thread and the file source is synchronous, so no DispatcherQueue marshalling
    /// is needed here — that only becomes a real concern with the live transport
    /// of experiment 03.
    /// </summary>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        var surfaces = new SurfaceManager();
        var dispatcher = new MessageDispatcher(surfaces);

        dispatcher.MessageRouted += line => LogList.Items.Add(line);
        dispatcher.RenderRequested += surface =>
        {
            var context = new RenderContext(new BindingResolver(surface.Data));
            SurfaceHost.Child = Renderer.Build(surface.Resolve(), context);
        };

        try
        {
            foreach (var message in A2uiStreamReader.Read(FixturePath))
            {
                dispatcher.Dispatch(message);
            }
        }
        catch (Exception ex)
        {
            // Surface failures in the log pane rather than crashing: a broken
            // fixture is a result worth reading, not a lost run.
            LogList.Items.Add($"error · {ex.Message}");
        }
    }
}

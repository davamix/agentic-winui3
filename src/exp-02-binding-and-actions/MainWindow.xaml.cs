using Exp02_BindingAndActions.Actions;
using Exp02_BindingAndActions.Protocol;
using Exp02_BindingAndActions.Rendering;
using Exp02_BindingAndActions.Surfaces;
using Microsoft.UI.Xaml;

namespace Exp02_BindingAndActions;

/// <summary>
/// The application window: a surface host on the left where the A2UI component
/// tree is rendered as native controls, and a log pane on the right listing every
/// message that crosses the boundary — inbound from the stream, outbound from a
/// user action, and inbound again from the scripted response.
/// </summary>
public sealed partial class MainWindow : Window
{
    private static readonly string SamplesDirectory =
        Path.Combine(AppContext.BaseDirectory, "Samples");

    /// <summary>
    /// The fixtures linked into the project by the csproj, so they sit next to the
    /// app (and inside the MSIX package) rather than at a path relative to the repo.
    /// </summary>
    private static readonly string FixturePath =
        Path.Combine(SamplesDirectory, "contact-form-bound.jsonl");

    /// <summary>
    /// Which canned stream the <see cref="ScriptedResponder"/> replays for which
    /// action name. This map is the entire "backend" of the experiment; experiment
    /// 04 replaces it with an agent and nothing else has to move.
    /// </summary>
    private static readonly Dictionary<string, string> Scripts = new(StringComparer.Ordinal)
    {
        ["submit"] = Path.Combine(SamplesDirectory, "submit-response.jsonl"),
    };

    /// <summary>
    /// Kept so the round trip can be *checked*, not just watched: if the status
    /// text changes while this is still the same instance, the update genuinely
    /// happened in place. Success criterion 5.
    /// </summary>
    private FrameworkElement? _rootControl;

    private int _renderCount;

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
    /// is needed here — and neither does the round trip, which runs start to finish
    /// inside the Click handler. That only becomes a real concern with the live
    /// transport of experiment 03.
    /// </summary>
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        var surfaces = new SurfaceManager();
        var dispatcher = new MessageDispatcher(surfaces);
        var responder = new ScriptedResponder(dispatcher, Scripts);

        dispatcher.MessageRouted += line => Log($"↓ {line}");
        responder.Responded += Log;
        dispatcher.RenderRequested += surface => Render(surface, responder);

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
            Log($"error · {ex.Message}");
        }
    }

    private void Render(Surface surface, ScriptedResponder responder)
    {
        var bindings = new BindingResolver(surface.Data);
        var actions = new ActionChannel(surface, bindings);

        actions.ActionSent += message =>
        {
            Log($"↑ action · {message.Action.Name} · from {message.Action.SourceComponentId}");
            Log(message.ToJson());

            // The whole round trip — response, data model write, binding update —
            // completes synchronously inside this call.
            responder.OnAction(message);

            Log($"= root unchanged: {ReferenceEquals(SurfaceHost.Child, _rootControl)}"
                + $" · renders: {_renderCount}");
        };

        _renderCount++;
        _rootControl = Renderer.Build(surface.Resolve(), new RenderContext(bindings, actions));
        SurfaceHost.Child = _rootControl;

        Log($"= render #{_renderCount} · {surface.ComponentCount} components");
    }

    private void Log(string line) => LogList.Items.Add(line);
}

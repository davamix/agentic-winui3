using Microsoft.UI.Xaml;

namespace Exp01_StaticRender;

/// <summary>
/// The application window: a surface host on the left where the A2UI component
/// tree is rendered as native controls, and a log pane on the right listing the
/// messages read from the stream.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
    }
}

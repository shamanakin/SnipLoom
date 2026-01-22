using System.Windows;

namespace SnipLoom;

public partial class App : Application
{
    public static Window? MainAppWindow { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainAppWindow = MainWindow;
    }
}

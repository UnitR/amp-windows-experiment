using Amp.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace Amp.App;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public static AppSettings Settings { get; } = AppSettings.Load();
    public static AmpService Amp { get; } = new(Settings);
    public static NotificationService Notifications { get; } = new();

    public App()
    {
        InitializeComponent();
        Notifications.Register();
        UnhandledException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine(e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window = new MainWindow();
        Window.Closed += (_, _) => Notifications.Unregister();
        Window.Activate();
    }
}

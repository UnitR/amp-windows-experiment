using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Amp.App.Services;

/// <summary>Native toast notifications through the Windows App SDK (works unpackaged).</summary>
public sealed class NotificationService
{
    private bool _registered;

    public event Action<string?>? Invoked;

    public void Register()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, args) =>
            {
                args.Arguments.TryGetValue("thread", out var thread);
                App.DispatcherQueue?.TryEnqueue(() => Invoked?.Invoke(thread));
            };
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Notification registration failed: {e.Message}");
        }
    }

    public void Unregister()
    {
        if (!_registered) return;
        try { AppNotificationManager.Default.Unregister(); } catch (Exception) { }
    }

    public void Show(string title, string body, string? threadId)
    {
        if (!_registered) return;
        try
        {
            var builder = new AppNotificationBuilder().AddText(title).AddText(body);
            if (threadId is not null) builder.AddArgument("thread", threadId);
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Notification failed: {e.Message}");
        }
    }
}

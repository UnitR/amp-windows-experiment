using Amp.App.Services;
using Amp.Sdk;
using Microsoft.UI.Xaml.Controls;

namespace Amp.App;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly AppSettings _settings;

    public SettingsDialog(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        CliPathBox.Text = settings.CliPath;
        DetectedPath.Text = AmpCliLocator.Find() is { } found ? $"Detected: {found.Command}" : "Not detected on this machine.";
        ApiKeyBox.Password = settings.ApiKey;
        WorkingDirectoryBox.Text = settings.DefaultWorkingDirectory;
        RunnerIdBox.Text = settings.RunnerId;

        ExecutorBox.ItemsSource = new[] { "local", "orb", "runner" };
        ExecutorBox.SelectedItem = settings.DefaultExecutor;
        ModeBox.ItemsSource = new[] { "low", "medium", "high", "ultra" };
        ModeBox.SelectedItem = settings.DefaultMode;
        EffortBox.ItemsSource = new[] { "default", "none", "minimal", "low", "medium", "high", "xhigh", "max" };
        EffortBox.SelectedItem = string.IsNullOrEmpty(settings.DefaultEffort) ? "default" : settings.DefaultEffort;
        VisibilityBox.ItemsSource = new[] { "workspace", "private", "unlisted", "group" };
        VisibilityBox.SelectedItem = settings.Visibility;

        ThinkingToggle.IsOn = settings.ShowThinking;
        NotificationsToggle.IsOn = settings.NotificationsEnabled;

        PrimaryButtonClick += (_, _) => Save();
    }

    private void Save()
    {
        _settings.CliPath = CliPathBox.Text.Trim();
        _settings.ApiKey = ApiKeyBox.Password.Trim();
        _settings.DefaultWorkingDirectory = WorkingDirectoryBox.Text.Trim();
        _settings.RunnerId = RunnerIdBox.Text.Trim();
        _settings.DefaultExecutor = ExecutorBox.SelectedItem as string ?? "local";
        _settings.DefaultMode = ModeBox.SelectedItem as string ?? "medium";
        _settings.DefaultEffort = EffortBox.SelectedItem is string effort && effort != "default" ? effort : "";
        _settings.Visibility = VisibilityBox.SelectedItem as string ?? "workspace";
        _settings.ShowThinking = ThinkingToggle.IsOn;
        _settings.NotificationsEnabled = NotificationsToggle.IsOn;
        _settings.Save();
    }
}

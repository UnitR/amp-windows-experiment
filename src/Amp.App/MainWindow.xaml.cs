using Amp.App.Services;
using Amp.App.ViewModels;
using Amp.Sdk;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Amp.App;

public sealed partial class MainWindow : Window
{
    private bool _isActive = true;

    public MainViewModel ViewModel { get; } = new(App.Amp, App.Settings);

    public string[] Executors { get; } = ["local", "orb", "runner"];
    public string[] Modes { get; } = ["low", "medium", "high", "ultra"];
    public string[] Efforts { get; } = ["default", "none", "minimal", "low", "medium", "high", "xhigh", "max"];

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));

        Activated += (_, e) => _isActive = e.WindowActivationState != WindowActivationState.Deactivated;
        ViewModel.TurnCompleted += OnTurnCompleted;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.Conversation)) Attach(ViewModel.Conversation);
        };
        Attach(ViewModel.Conversation);
        App.Notifications.Invoked += _ => Activate();

        _ = ViewModel.RefreshStatusAsync();
    }

    private void Attach(ConversationViewModel conversation)
    {
        conversation.ItemAppended += ScrollToEnd;
        ScrollToEnd();
    }

    private void ScrollToEnd() =>
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            TranscriptScroller.UpdateLayout();
            TranscriptScroller.ChangeView(null, TranscriptScroller.ScrollableHeight, null, disableAnimation: true);
        });

    private void OnTurnCompleted(ConversationViewModel conversation, ResultMessage? result)
    {
        if (_isActive || !App.Settings.NotificationsEnabled) return;
        var body = result switch
        {
            null => "The turn was cancelled or failed to start.",
            { IsError: true } => result.Error ?? "The turn failed.",
            _ => result.Result is { Length: > 0 } text ? (text.Length > 200 ? text[..200] + "…" : text) : "Turn finished.",
        };
        App.Notifications.Show(conversation.Title, body, conversation.ThreadId);
    }

    private void Composer_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
        if (shift) return;
        e.Handled = true;
        var send = ViewModel.Conversation.SendCommand;
        if (send.CanExecute(null)) send.Execute(null);
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) ViewModel.Conversation.WorkingDirectory = folder.Path;
    }

    private void OpenThread_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Conversation.ThreadUrl is { } url) CliSetup.OpenUrl(url);
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(App.Settings) { XamlRoot = Content.XamlRoot };
        if (await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
            await ViewModel.RefreshStatusAsync();
    }
}

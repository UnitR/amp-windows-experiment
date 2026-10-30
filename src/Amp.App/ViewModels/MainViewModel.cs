using System.Collections.ObjectModel;
using Amp.App.Services;
using Amp.Sdk;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Amp.App.ViewModels;

/// <summary>Window state: CLI readiness, the thread list, and the open conversation.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AmpService _amp;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _historyLoad;

    public MainViewModel(AmpService amp, AppSettings settings)
    {
        _amp = amp;
        _settings = settings;
        Conversation = NewConversation();
    }

    public ObservableCollection<ThreadSummary> Threads { get; } = [];

    [ObservableProperty]
    public partial ConversationViewModel Conversation { get; set; }

    [ObservableProperty]
    public partial ThreadSummary? SelectedThread { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(NeedsInstall))]
    [NotifyPropertyChangedFor(nameof(NeedsSignIn))]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    public partial CliStatus? Status { get; set; }

    /// <summary>Human-readable CLI state for the setup screen.</summary>
    public string StatusLine => Status switch
    {
        null => "",
        { Found: false } => "Amp CLI not found.",
        { Error: { } error } => $"Amp CLI {Status.Version} at {Status.Path}: {error}",
        _ => $"Amp CLI {Status.Version} found at {Status.Path}. You are not signed in.",
    };

    [ObservableProperty]
    public partial bool IsCheckingCli { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingThreads { get; set; }

    [ObservableProperty]
    public partial string? ThreadListError { get; set; }

    public bool IsReady => Status is { Found: true, SignedIn: true };
    public bool NeedsInstall => Status is { Found: false };
    public bool NeedsSignIn => Status is { Found: true, SignedIn: false };

    public event Action<ConversationViewModel, ResultMessage?>? TurnCompleted;

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        IsCheckingCli = true;
        try
        {
            Status = await _amp.CheckAsync();
        }
        finally
        {
            IsCheckingCli = false;
        }
        if (IsReady) await RefreshThreadsAsync();
    }

    [RelayCommand]
    public async Task RefreshThreadsAsync()
    {
        if (!IsReady) return;
        IsLoadingThreads = true;
        ThreadListError = null;
        try
        {
            var threads = await _amp.CreateClient().Threads.ListAsync(limit: 100);
            Threads.Clear();
            foreach (var t in threads) Threads.Add(t);
        }
        catch (AmpCliException e)
        {
            ThreadListError = e.Message;
        }
        finally
        {
            IsLoadingThreads = false;
        }
    }

    [RelayCommand]
    public void NewThread()
    {
        SelectedThread = null;
        Conversation = NewConversation();
    }

    [RelayCommand]
    public void Install() => CliSetup.Install();

    [RelayCommand]
    public void SignIn()
    {
        if (Status?.Path is { } path) CliSetup.Login(path);
    }

    partial void OnSelectedThreadChanged(ThreadSummary? value)
    {
        if (value is null || value.Id == Conversation.ThreadId) return;
        _historyLoad?.Cancel();
        _historyLoad = new CancellationTokenSource();
        Conversation = NewConversation();
        _ = Conversation.LoadHistoryAsync(value.Id, value.Title, _historyLoad.Token);
    }

    private ConversationViewModel NewConversation()
    {
        var conversation = new ConversationViewModel(_amp, _settings);
        conversation.TurnCompleted += OnTurnCompleted;
        return conversation;
    }

    private void OnTurnCompleted(ConversationViewModel conversation, ResultMessage? result)
    {
        TurnCompleted?.Invoke(conversation, result);
        // A first turn creates the thread; show it in the sidebar without re-querying the CLI.
        if (conversation.ThreadId is { } id && Threads.All(t => t.Id != id))
            Threads.Insert(0, new ThreadSummary(id, conversation.Title, DateTimeOffset.Now, null, conversation.Items.Count));
    }
}

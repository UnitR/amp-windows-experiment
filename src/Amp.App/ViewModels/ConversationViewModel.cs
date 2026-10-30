using System.Collections.ObjectModel;
using System.Text.Json;
using Amp.App.Models;
using Amp.App.Services;
using Amp.Sdk;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Amp.App.ViewModels;

/// <summary>One thread: its transcript, the composer draft, and the currently running turn.</summary>
public sealed partial class ConversationViewModel(AmpService amp, AppSettings settings) : ObservableObject
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private CancellationTokenSource? _turn;
    private readonly Dictionary<string, ToolCallItem> _toolCalls = new();

    public ObservableCollection<TranscriptItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreadUrl))]
    [NotifyPropertyChangedFor(nameof(HasThread))]
    public partial string? ThreadId { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "New thread";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Draft { get; set; } = "";

    [ObservableProperty]
    public partial bool IsLoadingHistory { get; set; }

    /// <summary>Per-thread overrides of the settings defaults. Null means "use settings".</summary>
    [ObservableProperty]
    public partial string Mode { get; set; } = settings.DefaultMode;

    [ObservableProperty]
    public partial string Effort { get; set; } = string.IsNullOrEmpty(settings.DefaultEffort) ? "default" : settings.DefaultEffort;

    [ObservableProperty]
    public partial string Executor { get; set; } = settings.DefaultExecutor;

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; } = settings.DefaultWorkingDirectory;

    public string? ThreadUrl => ThreadId is null ? null : $"https://ampcode.com/threads/{ThreadId}";
    public bool HasThread => ThreadId is not null;

    /// <summary>Raised on the UI thread when a turn ends; the result is null when the turn was cancelled or failed to start.</summary>
    public event Action<ConversationViewModel, ResultMessage?>? TurnCompleted;

    /// <summary>Raised when an item is appended so the view can keep the newest row in view.</summary>
    public event Action? ItemAppended;

    private bool CanSend() => !IsRunning && !string.IsNullOrWhiteSpace(Draft);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync() => RunTurnAsync(Draft.Trim());

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _turn?.Cancel();

    public async Task RunTurnAsync(string prompt)
    {
        Draft = "";
        Append(new UserTurnItem(prompt));
        if (Title == "New thread") Title = prompt.Length > 60 ? prompt[..60] + "…" : prompt;

        var isNew = ThreadId is null;
        var options = new AmpOptions
        {
            Cwd = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
            Mode = string.IsNullOrWhiteSpace(Mode) ? null : Mode,
            Effort = string.IsNullOrWhiteSpace(Effort) || Effort == "default" ? null : Effort,
            Executor = Executor switch { "orb" => AmpExecutor.Orb, "runner" => AmpExecutor.Runner, _ => AmpExecutor.Local },
            RunnerId = Executor == "runner" && !string.IsNullOrWhiteSpace(settings.RunnerId) ? settings.RunnerId : null,
            ContinueThread = ThreadId,
            NoArchiveAfterExecute = isNew,
            Visibility = isNew ? settings.Visibility : null,
            Thinking = settings.ShowThinking,
            Env = amp.Environment(),
        };

        IsRunning = true;
        _turn = new CancellationTokenSource();
        ResultMessage? result = null;
        try
        {
            var client = amp.CreateClient();
            await foreach (var message in client.ExecuteAsync(prompt, options, _turn.Token))
            {
                switch (message)
                {
                    case SystemMessage system:
                        ThreadId ??= system.SessionId;
                        break;
                    case AssistantMessage assistant:
                        foreach (var block in assistant.Content) AppendAssistantBlock(block);
                        break;
                    case UserMessage user:
                        foreach (var block in user.Content.OfType<ToolResultContent>()) ApplyToolResult(block);
                        break;
                    case ResultMessage r:
                        result = r;
                        if (r.IsError) Append(new StatusItem(r.Error ?? r.Result ?? "The turn failed.", isError: true));
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Append(new StatusItem("Cancelled."));
        }
        catch (AmpCliException e)
        {
            Append(new StatusItem(e.Message, isError: true));
        }
        finally
        {
            foreach (var call in _toolCalls.Values.Where(c => c.IsRunning)) call.IsRunning = false;
            _toolCalls.Clear();
            _turn.Dispose();
            _turn = null;
            IsRunning = false;
        }
        TurnCompleted?.Invoke(this, result);
    }

    private void AppendAssistantBlock(ContentBlock block)
    {
        switch (block)
        {
            case TextContent text when !string.IsNullOrWhiteSpace(text.Text):
                Append(new AssistantTextItem { Markdown = text.Text });
                break;
            case ThinkingContent thinking when settings.ShowThinking && !string.IsNullOrWhiteSpace(thinking.Thinking):
                Append(new ThinkingItem { Text = thinking.Thinking });
                break;
            case ToolUseContent tool:
                var item = new ToolCallItem
                {
                    ToolUseId = tool.Id,
                    Name = tool.Name,
                    Summary = SummarizeInput(tool.Input),
                    Input = tool.Input.ValueKind == JsonValueKind.Undefined ? "" : JsonSerializer.Serialize(tool.Input, Pretty),
                };
                _toolCalls[tool.Id] = item;
                Append(item);
                break;
        }
    }

    private void ApplyToolResult(ToolResultContent result)
    {
        if (!_toolCalls.TryGetValue(result.ToolUseId, out var call)) return;
        call.Result = result.Content;
        call.IsError = result.IsError;
        call.IsRunning = false;
    }

    /// <summary>Picks the most descriptive string argument (command, path, query…) for the tool card header.</summary>
    private static string SummarizeInput(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object) return "";
        foreach (var key in new[] { "command", "cmd", "path", "file_path", "pattern", "query", "url", "prompt", "description", "status", "name" })
            if (input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s)
                return s.Length > 120 ? s[..120] + "…" : s.ReplaceLineEndings(" ");
        foreach (var property in input.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { Length: > 0 } s)
                return s.Length > 120 ? s[..120] + "…" : s.ReplaceLineEndings(" ");
        return "";
    }

    /// <summary>Replaces the transcript with the thread's history from <c>amp threads markdown</c>.</summary>
    public async Task LoadHistoryAsync(string threadId, string title, CancellationToken cancellationToken)
    {
        ThreadId = threadId;
        Title = string.IsNullOrWhiteSpace(title) ? threadId : title;
        Items.Clear();
        IsLoadingHistory = true;
        try
        {
            var markdown = await amp.CreateClient().Threads.MarkdownAsync(threadId, cancellationToken);
            foreach (var item in ThreadMarkdown.Parse(markdown)) Items.Add(item);
            ItemAppended?.Invoke();
        }
        catch (AmpCliException e)
        {
            Items.Add(new StatusItem(e.Message, isError: true));
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private void Append(TranscriptItem item)
    {
        Items.Add(item);
        ItemAppended?.Invoke();
    }
}

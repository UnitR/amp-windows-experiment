using CommunityToolkit.Mvvm.ComponentModel;

namespace Amp.App.Models;

/// <summary>One row of the conversation transcript.</summary>
public abstract partial class TranscriptItem : ObservableObject;

public sealed partial class UserTurnItem(string text) : TranscriptItem
{
    public string Text { get; } = text;
}

public sealed partial class AssistantTextItem : TranscriptItem
{
    [ObservableProperty]
    public partial string Markdown { get; set; } = "";
}

public sealed partial class ThinkingItem : TranscriptItem
{
    [ObservableProperty]
    public partial string Text { get; set; } = "";
}

public sealed partial class ToolCallItem : TranscriptItem
{
    public required string ToolUseId { get; init; }
    public required string Name { get; init; }

    /// <summary>Short one-line description shown in the header, e.g. the shell command or file path.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    /// <summary>Full input, pretty-printed JSON.</summary>
    [ObservableProperty]
    public partial string Input { get; set; } = "";

    [ObservableProperty]
    public partial string Result { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    public partial bool IsRunning { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    public partial bool IsError { get; set; }

    public bool HasResult => Result.Length > 0;

    /// <summary>Segoe Fluent Icons glyph: hourglass while running, error or checkmark afterwards.</summary>
    public string StatusGlyph => IsRunning ? "\uE916" : IsError ? "\uEA39" : "\uE73E";

    partial void OnResultChanged(string value) => OnPropertyChanged(nameof(HasResult));
}

/// <summary>A note in the transcript: errors, cancellation, loaded-history markers, turn statistics.</summary>
public sealed partial class StatusItem(string text, bool isError = false) : TranscriptItem
{
    public string Text { get; } = text;
    public bool IsError { get; } = isError;
}

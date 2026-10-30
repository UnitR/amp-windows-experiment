using Amp.App.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Amp.App.Controls;

/// <summary>Picks the transcript row template by item type.</summary>
public sealed partial class TranscriptTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTurn { get; set; }
    public DataTemplate? AssistantText { get; set; }
    public DataTemplate? Thinking { get; set; }
    public DataTemplate? ToolCall { get; set; }
    public DataTemplate? Status { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        UserTurnItem => UserTurn,
        AssistantTextItem => AssistantText,
        ThinkingItem => Thinking,
        ToolCallItem => ToolCall,
        StatusItem => Status,
        _ => null,
    };
}

/// <summary>Static helpers for x:Bind function bindings.</summary>
public static class Formatting
{
    public static string Relative(DateTimeOffset? time)
    {
        if (time is not { } t) return "";
        var age = DateTimeOffset.Now - t;
        return age.TotalMinutes < 1 ? "just now"
            : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} min ago"
            : age.TotalDays < 1 ? $"{(int)age.TotalHours} h ago"
            : age.TotalDays < 7 ? $"{(int)age.TotalDays} d ago"
            : t.LocalDateTime.ToString("d MMM yyyy");
    }

    public static string TitleOrId(string title, string id) => string.IsNullOrWhiteSpace(title) ? id : title;

    public static bool Not(bool value) => !value;

    public static bool IsNotEmpty(string? value) => !string.IsNullOrEmpty(value);

    public static bool IsEmpty(int count) => count == 0;

    public static InfoBarSeverity Severity(bool isError) => isError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
}

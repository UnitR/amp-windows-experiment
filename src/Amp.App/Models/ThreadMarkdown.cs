namespace Amp.App.Models;

/// <summary>
/// Splits the output of <c>amp threads markdown</c> into transcript rows. The format is a YAML front matter, a
/// <c># Title</c> heading, then alternating <c>## User</c> / <c>## Assistant</c> sections. Tool calls and results
/// appear as <c>**Tool Use:**</c> / <c>**Tool Result:**</c> paragraphs and are kept as Markdown.
/// </summary>
public static class ThreadMarkdown
{
    public static IReadOnlyList<TranscriptItem> Parse(string markdown)
    {
        var items = new List<TranscriptItem>();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var i = 0;

        if (lines.Length > 0 && lines[0] == "---")
        {
            i = 1;
            while (i < lines.Length && lines[i] != "---") i++;
            i++;
        }

        string? role = null;
        var body = new List<string>();
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line is "## User" or "## Assistant")
            {
                Flush(role, body, items);
                role = line[3..];
                body.Clear();
                continue;
            }
            if (role is null && line.StartsWith("# ")) continue; // thread title heading
            if (role is not null) body.Add(line);
        }
        Flush(role, body, items);
        return items;
    }

    private static void Flush(string? role, List<string> body, List<TranscriptItem> items)
    {
        if (role is null) return;
        var text = string.Join('\n', body).Trim();
        if (text.Length == 0) return;
        // Tool results are recorded under the user role but belong to the agent's work, not to what the person typed.
        if (role == "User" && !text.StartsWith("**Tool Result:**", StringComparison.Ordinal))
            items.Add(new UserTurnItem(text));
        else
            items.Add(new AssistantTextItem { Markdown = text });
    }
}

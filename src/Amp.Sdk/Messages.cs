using System.Text.Json;

namespace Amp.Sdk;

/// <summary>Base of every message the CLI streams in <c>--stream-json</c> mode.</summary>
public abstract record StreamMessage
{
    public required string Type { get; init; }
    public string? SessionId { get; init; }

    /// <summary>The raw JSON object, for fields this SDK does not model.</summary>
    public JsonElement Raw { get; init; }
}

public sealed record McpServerStatus(string Name, string Status);

/// <summary>First message of a session: thread ID (<see cref="StreamMessage.SessionId"/>), tools, MCP servers.</summary>
public sealed record SystemMessage : StreamMessage
{
    public string? Subtype { get; init; }
    public string? Cwd { get; init; }
    public IReadOnlyList<string> Tools { get; init; } = [];
    public IReadOnlyList<McpServerStatus> McpServers { get; init; } = [];
}

public sealed record Usage
{
    public long InputTokens { get; init; }
    public long? CacheCreationInputTokens { get; init; }
    public long? CacheReadInputTokens { get; init; }
    public long OutputTokens { get; init; }
    public string? ServiceTier { get; init; }
}

/// <summary>A content block inside an assistant or user message.</summary>
public abstract record ContentBlock
{
    public required string Type { get; init; }
    public JsonElement Raw { get; init; }
}

public sealed record TextContent : ContentBlock
{
    public required string Text { get; init; }
}

public sealed record ThinkingContent : ContentBlock
{
    public required string Thinking { get; init; }
}

public sealed record ToolUseContent : ContentBlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public JsonElement Input { get; init; }
}

public sealed record ToolResultContent : ContentBlock
{
    public required string ToolUseId { get; init; }
    /// <summary>The result as text. Array-shaped results are flattened to their text parts.</summary>
    public string Content { get; init; } = "";
    public bool IsError { get; init; }
}

/// <summary>A content block whose type this SDK does not know.</summary>
public sealed record UnknownContent : ContentBlock;

public sealed record AssistantMessage : StreamMessage
{
    public string? MessageId { get; init; }
    public string? Model { get; init; }
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];
    public string? StopReason { get; init; }
    public Usage? Usage { get; init; }
    public string? ParentToolUseId { get; init; }

    public string Text => string.Concat(Content.OfType<TextContent>().Select(t => t.Text));
}

public sealed record UserMessage : StreamMessage
{
    public IReadOnlyList<ContentBlock> Content { get; init; } = [];
    public string? ParentToolUseId { get; init; }

    public string Text => string.Concat(Content.OfType<TextContent>().Select(t => t.Text));
}

/// <summary>Final message of a turn. <see cref="IsError"/> distinguishes success from failure.</summary>
public sealed record ResultMessage : StreamMessage
{
    public string? Subtype { get; init; }
    public bool IsError { get; init; }
    /// <summary>Final assistant text on success.</summary>
    public string? Result { get; init; }
    /// <summary>Error description on failure.</summary>
    public string? Error { get; init; }
    public long DurationMs { get; init; }
    public int NumTurns { get; init; }
    public Usage? Usage { get; init; }
    public IReadOnlyList<string> PermissionDenials { get; init; } = [];
}

/// <summary>A message whose <c>type</c> this SDK does not know.</summary>
public sealed record UnknownMessage : StreamMessage;

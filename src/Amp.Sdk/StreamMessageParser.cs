using System.Text.Json;

namespace Amp.Sdk;

/// <summary>Parses one JSON line of <c>--stream-json</c> output. Unknown types and fields are preserved, never rejected.</summary>
public static class StreamMessageParser
{
    public static StreamMessage Parse(string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException e)
        {
            throw new AmpCliException($"Failed to parse JSON response, raw line: {line}", e);
        }

        var root = doc.RootElement.Clone();
        var type = GetString(root, "type") ?? "";
        var sessionId = GetString(root, "session_id");

        return type switch
        {
            "system" => new SystemMessage
            {
                Type = type, SessionId = sessionId, Raw = root,
                Subtype = GetString(root, "subtype"),
                Cwd = GetString(root, "cwd"),
                Tools = GetStringArray(root, "tools"),
                McpServers = root.TryGetProperty("mcp_servers", out var servers) && servers.ValueKind == JsonValueKind.Array
                    ? servers.EnumerateArray().Select(s => new McpServerStatus(GetString(s, "name") ?? "", GetString(s, "status") ?? "")).ToList()
                    : [],
            },
            "assistant" => ParseAssistant(root, type, sessionId),
            "user" => new UserMessage
            {
                Type = type, SessionId = sessionId, Raw = root,
                Content = ParseContent(root),
                ParentToolUseId = GetString(root, "parent_tool_use_id"),
            },
            "result" => new ResultMessage
            {
                Type = type, SessionId = sessionId, Raw = root,
                Subtype = GetString(root, "subtype"),
                IsError = root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True,
                Result = GetString(root, "result"),
                Error = GetString(root, "error"),
                DurationMs = GetInt64(root, "duration_ms"),
                NumTurns = (int)GetInt64(root, "num_turns"),
                Usage = ParseUsage(root),
                PermissionDenials = GetStringArray(root, "permission_denials"),
            },
            _ => new UnknownMessage { Type = type, SessionId = sessionId, Raw = root },
        };
    }

    private static AssistantMessage ParseAssistant(JsonElement root, string type, string? sessionId)
    {
        var hasMessage = root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object;
        return new AssistantMessage
        {
            Type = type, SessionId = sessionId, Raw = root,
            MessageId = hasMessage ? GetString(message, "id") : null,
            Model = hasMessage ? GetString(message, "model") : null,
            StopReason = hasMessage ? GetString(message, "stop_reason") : null,
            Usage = hasMessage ? ParseUsage(message) : null,
            Content = ParseContent(root),
            ParentToolUseId = GetString(root, "parent_tool_use_id"),
        };
    }

    private static IReadOnlyList<ContentBlock> ParseContent(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return [];
        if (!message.TryGetProperty("content", out var content)) return [];

        if (content.ValueKind == JsonValueKind.String)
            return [new TextContent { Type = "text", Text = content.GetString() ?? "", Raw = content }];
        if (content.ValueKind != JsonValueKind.Array) return [];

        return content.EnumerateArray().Select(ParseBlock).ToList();
    }

    private static ContentBlock ParseBlock(JsonElement block)
    {
        var type = GetString(block, "type") ?? "";
        return type switch
        {
            "text" => new TextContent { Type = type, Raw = block, Text = GetString(block, "text") ?? "" },
            "thinking" => new ThinkingContent { Type = type, Raw = block, Thinking = GetString(block, "thinking") ?? "" },
            "tool_use" => new ToolUseContent
            {
                Type = type, Raw = block,
                Id = GetString(block, "id") ?? "",
                Name = GetString(block, "name") ?? "",
                Input = block.TryGetProperty("input", out var input) ? input : default,
            },
            "tool_result" => new ToolResultContent
            {
                Type = type, Raw = block,
                ToolUseId = GetString(block, "tool_use_id") ?? "",
                Content = FlattenToolResult(block),
                IsError = block.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True,
            },
            _ => new UnknownContent { Type = type, Raw = block },
        };
    }

    private static string FlattenToolResult(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var content)) return "";
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? "",
            JsonValueKind.Array => string.Join("\n", content.EnumerateArray().Select(part =>
                part.ValueKind == JsonValueKind.String ? part.GetString() ?? "" : GetString(part, "text") ?? part.GetRawText())),
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => content.GetRawText(),
        };
    }

    private static Usage? ParseUsage(JsonElement parent)
    {
        if (!parent.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        return new Usage
        {
            InputTokens = GetInt64(usage, "input_tokens"),
            OutputTokens = GetInt64(usage, "output_tokens"),
            CacheCreationInputTokens = usage.TryGetProperty("cache_creation_input_tokens", out var cc) && cc.ValueKind == JsonValueKind.Number ? cc.GetInt64() : null,
            CacheReadInputTokens = usage.TryGetProperty("cache_read_input_tokens", out var cr) && cr.ValueKind == JsonValueKind.Number ? cr.GetInt64() : null,
            ServiceTier = GetString(usage, "service_tier"),
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetInt64(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)
            ? n
            : 0;

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToList()
            : [];
}

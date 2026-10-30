using System.Text.Encodings.Web;
using System.Text.Json;

namespace Amp.Sdk;

/// <summary>A user message for streaming input (<c>--stream-json-input</c>).</summary>
public sealed record UserInputMessage(string Text, string? RequestId = null)
{
    /// <summary>Serializes to one JSON line in the shape the CLI expects: <c>{"type":"user","request_id":…,"message":{"role":"user","content":[{"type":"text","text":…}]}}</c>.</summary>
    public string ToJsonLine()
    {
        if (RequestId is not null && (RequestId.Length is < 1 or > 256))
            throw new ArgumentException("RequestId must be 1–256 characters.", nameof(RequestId));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "user");
            if (RequestId is not null) writer.WriteString("request_id", RequestId);
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteStartArray("content");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", Text);
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        // JSON Lines must not contain raw line separators.
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029") + "\n";
    }
}

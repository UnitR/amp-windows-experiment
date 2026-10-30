using System.Text.Json;

namespace Amp.Sdk.Tests;

public class UserInputMessageTests
{
    [Fact]
    public void SerializesToCliShape()
    {
        var line = new UserInputMessage("hello \"there\"", "req-1").ToJsonLine();
        Assert.EndsWith("\n", line);
        Assert.Equal("""{"type":"user","request_id":"req-1","message":{"role":"user","content":[{"type":"text","text":"hello \"there\""}]}}""", line.TrimEnd('\n'));
    }

    [Fact]
    public void OmitsRequestIdWhenNotSet()
    {
        using var doc = JsonDocument.Parse(new UserInputMessage("hi").ToJsonLine());
        Assert.False(doc.RootElement.TryGetProperty("request_id", out _));
    }

    [Fact]
    public void EscapesUnicodeLineSeparatorsAndKeepsOneLine()
    {
        var line = new UserInputMessage("a\u2028b\u2029c\nd").ToJsonLine();
        Assert.Single(line.TrimEnd('\n').Split('\n'));
        Assert.DoesNotContain("\u2028", line);
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("a\u2028b\u2029c\nd", doc.RootElement.GetProperty("message").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void RejectsRequestIdOutsideLengthLimit()
    {
        Assert.Throws<ArgumentException>(() => new UserInputMessage("x", new string('a', 257)).ToJsonLine());
        Assert.Throws<ArgumentException>(() => new UserInputMessage("x", "").ToJsonLine());
    }
}

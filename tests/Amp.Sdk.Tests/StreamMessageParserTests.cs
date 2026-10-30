namespace Amp.Sdk.Tests;

public class StreamMessageParserTests
{
    private static readonly string[] FixtureLines =
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "shell-turn.jsonl"));

    [Fact]
    public void ParsesRealTurnInOrder()
    {
        var messages = FixtureLines.Select(StreamMessageParser.Parse).ToList();

        Assert.Collection(messages,
            m =>
            {
                var system = Assert.IsType<SystemMessage>(m);
                Assert.Equal("init", system.Subtype);
                Assert.StartsWith("T-", system.SessionId);
                Assert.Contains("shell_command", system.Tools);
                Assert.Equal("/tmp/ampsdk/fx", system.Cwd);
            },
            m =>
            {
                var user = Assert.IsType<UserMessage>(m);
                Assert.StartsWith("Run the shell command", user.Text);
            },
            m =>
            {
                var assistant = Assert.IsType<AssistantMessage>(m);
                var toolUse = Assert.IsType<ToolUseContent>(Assert.Single(assistant.Content));
                Assert.Equal("shell_command", toolUse.Name);
                Assert.Equal("echo hello-from-fixture", toolUse.Input.GetProperty("command").GetString());
                Assert.Equal("tool_use", assistant.StopReason);
            },
            m =>
            {
                var user = Assert.IsType<UserMessage>(m);
                var result = Assert.IsType<ToolResultContent>(Assert.Single(user.Content));
                Assert.StartsWith("TU-", result.ToolUseId);
                Assert.False(result.IsError);
                Assert.Contains("hello-from-fixture", result.Content);
            },
            m =>
            {
                var assistant = Assert.IsType<AssistantMessage>(m);
                Assert.Equal("done", assistant.Text);
                Assert.Equal("end_turn", assistant.StopReason);
                Assert.NotNull(assistant.Usage);
            },
            m =>
            {
                var result = Assert.IsType<ResultMessage>(m);
                Assert.False(result.IsError);
                Assert.Equal("done", result.Result);
                Assert.Equal(2, result.NumTurns);
                Assert.True(result.DurationMs > 0);
            });

        Assert.All(messages, m => Assert.Equal(messages[0].SessionId, m.SessionId));
    }

    [Fact]
    public void ErrorResultCarriesErrorText()
    {
        var m = StreamMessageParser.Parse("""{"type":"result","subtype":"error_during_execution","session_id":"T-1","is_error":true,"error":"boom","duration_ms":5,"num_turns":1}""");
        var result = Assert.IsType<ResultMessage>(m);
        Assert.True(result.IsError);
        Assert.Equal("boom", result.Error);
        Assert.Null(result.Result);
    }

    [Fact]
    public void ArrayToolResultIsFlattenedToText()
    {
        var m = StreamMessageParser.Parse("""{"type":"user","session_id":"T-1","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"TU-1","is_error":true,"content":[{"type":"text","text":"line one"},{"type":"text","text":"line two"}]}]},"parent_tool_use_id":null}""");
        var result = Assert.IsType<ToolResultContent>(Assert.Single(Assert.IsType<UserMessage>(m).Content));
        Assert.Equal("line one\nline two", result.Content);
        Assert.True(result.IsError);
    }

    [Fact]
    public void ThinkingBlockIsParsed()
    {
        var m = StreamMessageParser.Parse("""{"type":"assistant","session_id":"T-1","message":{"role":"assistant","content":[{"type":"thinking","thinking":"hmm"},{"type":"text","text":"ok"}]}}""");
        var assistant = Assert.IsType<AssistantMessage>(m);
        Assert.Equal("hmm", Assert.IsType<ThinkingContent>(assistant.Content[0]).Thinking);
        Assert.Equal("ok", assistant.Text);
    }

    [Fact]
    public void UnknownTypesAreKeptNotRejected()
    {
        var m = StreamMessageParser.Parse("""{"type":"progress","session_id":"T-1","percent":40}""");
        var unknown = Assert.IsType<UnknownMessage>(m);
        Assert.Equal(40, unknown.Raw.GetProperty("percent").GetInt32());

        var block = StreamMessageParser.Parse("""{"type":"assistant","session_id":"T-1","message":{"role":"assistant","content":[{"type":"image","source":"x"}]}}""");
        Assert.IsType<UnknownContent>(Assert.Single(Assert.IsType<AssistantMessage>(block).Content));
    }

    [Fact]
    public void InvalidJsonThrowsAmpCliException()
    {
        var e = Assert.Throws<AmpCliException>(() => StreamMessageParser.Parse("not json"));
        Assert.Contains("not json", e.Message);
    }
}

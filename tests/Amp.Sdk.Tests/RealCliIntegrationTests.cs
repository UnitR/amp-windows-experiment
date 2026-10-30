namespace Amp.Sdk.Tests;

/// <summary>
/// Exercises the real Amp CLI. Opt in with <c>AMP_SDK_INTEGRATION=1</c>; needs a signed-in CLI or <c>AMP_API_KEY</c>.
/// Creates private threads labelled <c>sdk-integration</c>.
/// </summary>
public class RealCliIntegrationTests
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("AMP_SDK_INTEGRATION") == "1";
    private static readonly AmpOptions Base = new() { Mode = "low", Visibility = "private", NoArchiveAfterExecute = true, Labels = ["sdk-integration"] };

    [Fact]
    public async Task ExecuteThenContinueKeepsThreadState()
    {
        Assert.SkipUnless(Enabled, "Set AMP_SDK_INTEGRATION=1 to run against the real CLI.");
        var client = new AmpClient();

        var first = await Collect(client.ExecuteAsync("Reply with exactly the single word: pong", Base with { Title = "SDK integration: continue" }));
        var threadId = Assert.IsType<SystemMessage>(first[0]).SessionId!;
        Assert.StartsWith("T-", threadId);
        var firstResult = Assert.IsType<ResultMessage>(first[^1]);
        Assert.False(firstResult.IsError, firstResult.Error);
        Assert.Contains("pong", firstResult.Result, StringComparison.OrdinalIgnoreCase);

        var second = await Collect(client.ExecuteAsync("Repeat the single word you replied with before, nothing else.", Base with { ContinueThread = threadId }));
        Assert.Equal(threadId, second[0].SessionId);
        var secondResult = Assert.IsType<ResultMessage>(second[^1]);
        Assert.False(secondResult.IsError, secondResult.Error);
        Assert.Contains("pong", secondResult.Result, StringComparison.OrdinalIgnoreCase);

        var threads = await client.Threads.ListAsync(limit: 20);
        Assert.Contains(threads, t => t.Id == threadId);

        // The thread syncs to the server shortly after the turn ends; poll rather than assert on the first fetch.
        var markdown = "";
        for (var attempt = 0; attempt < 10 && !markdown.Contains("pong", StringComparison.OrdinalIgnoreCase); attempt++)
        {
            if (attempt > 0) await Task.Delay(1000);
            markdown = await client.Threads.MarkdownAsync(threadId);
        }
        Assert.Contains($"threadId: {threadId}", markdown);
        Assert.Contains("pong", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamingInputRunsTwoTurnsInOneProcess()
    {
        Assert.SkipUnless(Enabled, "Set AMP_SDK_INTEGRATION=1 to run against the real CLI.");
        var client = new AmpClient();

        var secondTurnGate = new TaskCompletionSource();
        async IAsyncEnumerable<UserInputMessage> Prompts()
        {
            yield return new UserInputMessage("Reply with exactly the single word: alpha", "turn-1");
            await secondTurnGate.Task;
            yield return new UserInputMessage("Now reply with exactly the single word: beta", "turn-2");
        }

        // The CLI queues stdin messages as turns and emits one result when stdin closes, so the turn boundary is the
        // assistant's end_turn message, not a result message.
        var assistantTexts = new List<string>();
        var results = new List<ResultMessage>();
        var sessionIds = new HashSet<string>();
        await foreach (var m in client.ExecuteAsync(Prompts(), Base with { Title = "SDK integration: streaming" }))
        {
            if (m.SessionId is not null) sessionIds.Add(m.SessionId);
            switch (m)
            {
                case AssistantMessage { StopReason: "end_turn" } a:
                    assistantTexts.Add(a.Text);
                    if (assistantTexts.Count == 1) secondTurnGate.SetResult();
                    break;
                case ResultMessage r:
                    results.Add(r);
                    break;
            }
        }

        Assert.Single(sessionIds);
        Assert.Equal(2, assistantTexts.Count);
        Assert.Contains("alpha", assistantTexts[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("beta", assistantTexts[1], StringComparison.OrdinalIgnoreCase);
        var result = Assert.Single(results);
        Assert.False(result.IsError, result.Error);
    }

    [Fact]
    public async Task VersionAndAccountsWork()
    {
        Assert.SkipUnless(Enabled, "Set AMP_SDK_INTEGRATION=1 to run against the real CLI.");
        var client = new AmpClient();
        var version = await client.GetVersionAsync();
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
        _ = await client.ListAsync();
    }

    private static async Task<List<StreamMessage>> Collect(IAsyncEnumerable<StreamMessage> stream)
    {
        var list = new List<StreamMessage>();
        await foreach (var m in stream) list.Add(m);
        return list;
    }
}

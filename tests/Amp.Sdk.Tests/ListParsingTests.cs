namespace Amp.Sdk.Tests;

public class ListParsingTests
{
    [Fact]
    public void ParsesThreadList()
    {
        var threads = ThreadsApi.ParseList("""
            [
              {"id":"T-01a1239f-4d86-75d3-9438-d023b9d6b0f0","title":"Sentry Digest","updated":"2026-10-28T20:07:47.535Z","tree":"file:///home/user/workspace/repo","messageCount":24},
              {"id":"T-2","title":"","updated":"garbage","messageCount":0},
              {"title":"no id"}
            ]
            """);

        Assert.Equal(2, threads.Count);
        Assert.Equal("Sentry Digest", threads[0].Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 20, 7, 47, 535, TimeSpan.Zero), threads[0].Updated);
        Assert.Equal("file:///home/user/workspace/repo", threads[0].Tree);
        Assert.Equal(24, threads[0].MessageCount);
        Assert.Equal("https://ampcode.com/threads/T-01a1239f-4d86-75d3-9438-d023b9d6b0f0", threads[0].Url);
        Assert.Null(threads[1].Updated);
    }

    [Fact]
    public void ParsesAccounts()
    {
        Assert.Empty(AccountsApi.ParseAccounts("""{"accounts":[]}"""));
        var accounts = AccountsApi.ParseAccounts("""{"accounts":[{"email":"a@b.c","userId":"user_1","server":"https://ampcode.com","active":true}]}""");
        var account = Assert.Single(accounts);
        Assert.Equal("a@b.c", account.Email);
        Assert.Equal("user_1", account.UserId);
        Assert.True(account.Active);
    }
}

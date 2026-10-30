# Amp for Windows

A native Windows client for the [Amp](https://ampcode.com) coding agent, built with WinUI 3 on the Windows App SDK.
No web views anywhere: the transcript, Markdown, tool cards and settings are all XAML.

The repository has two parts:

- `src/Amp.Sdk` — a dependency-free C# port of the [`@ampcode/sdk`](https://ampcode.com/docs/sdk) TypeScript SDK.
  It drives the `amp` CLI (`--execute --stream-json`), parses the JSON-Lines stream, and wraps `amp threads …`
  and `amp account list`. It targets `net10.0` and works on Windows, Linux and macOS.
- `src/Amp.App` — the WinUI 3 app (`net10.0-windows`, x64 and ARM64).

## Requirements

- Windows 10 2004 (19041) or later; Windows 11 recommended for Mica.
- The Amp CLI. The app offers to install it on first run, or run in PowerShell:
  `irm https://ampcode.com/install.ps1 | iex`
- Sign-in: either click **Sign in** (runs `amp login`, opens your browser) or paste an API key in **Settings**.
  The key is stored DPAPI-encrypted under `%LOCALAPPDATA%\Amp\settings.json`.

## Running a build

The GitHub Actions workflow publishes a self-contained, unpackaged build per architecture
(`Amp-windows-x64`, `Amp-windows-ARM64`). Download the artifact, unzip it, run `Amp.exe`. Nothing to install
and no certificate needed.

## Building locally

Needs the .NET 10 SDK on Windows. Visual Studio is not required.

```powershell
dotnet test --project tests/Amp.Sdk.Tests
dotnet build src/Amp.App -c Debug -p:Platform=x64
dotnet publish src/Amp.App -c Release -p:Platform=x64 -r win-x64 -o artifacts/x64
```

Opt-in integration tests against the real CLI (they create private threads labelled `sdk-integration`):

```powershell
$env:AMP_SDK_INTEGRATION = "1"
dotnet test --project tests/Amp.Sdk.Tests -- --filter-class "*RealCliIntegrationTests"
```

## How the app talks to Amp

Each message you send starts one `amp --execute --stream-json` process (with `threads continue <id>` for follow-ups),
exactly like the official SDK. Executor (local / orb / runner), mode and effort are per-thread pickers above the
transcript. Remote executors need an API key or a signed-in CLI. Threads created from the app are kept
(`--no-archive-after-execute`).

## Using the SDK on its own

```csharp
using Amp.Sdk;

var client = new AmpClient(); // finds amp via AMP_CLI_PATH, ~/.amp/bin, then PATH
await foreach (var message in client.ExecuteAsync("Summarise this repo", new AmpOptions { Cwd = @"C:\src\repo", Mode = "medium" }))
{
    if (message is AssistantMessage a) Console.WriteLine(a.Text);
    if (message is ResultMessage r) Console.WriteLine(r.IsError ? $"Failed: {r.Error}" : "Done");
}

var threads = await client.Threads.ListAsync(limit: 20);
```

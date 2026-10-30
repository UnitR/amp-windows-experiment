using System.Diagnostics;

namespace Amp.App.Services;

/// <summary>Launches the interactive parts of CLI setup in their own console windows.</summary>
public static class CliSetup
{
    public const string InstallCommand = "irm https://ampcode.com/install.ps1 | iex";

    /// <summary>Runs the official installer in a visible PowerShell window.</summary>
    public static void Install() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoExit -ExecutionPolicy Bypass -Command \"{InstallCommand}\"",
            UseShellExecute = true,
        });

    /// <summary>Runs <c>amp login</c> in a console window; it opens the system browser and waits for the callback.</summary>
    public static void Login(string cliPath) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = "login",
            UseShellExecute = true,
        });

    public static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
}

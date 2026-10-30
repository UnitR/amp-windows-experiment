using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amp.App.Services;

/// <summary>Persisted app settings in <c>%LOCALAPPDATA%\Amp\settings.json</c>. The API key is DPAPI-protected per user.</summary>
public sealed class AppSettings
{
    public static string Directory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Amp");
    private static string FilePath => Path.Combine(Directory, "settings.json");

    /// <summary>Explicit path to amp.exe. Empty means auto-detect.</summary>
    public string CliPath { get; set; } = "";
    public string DefaultWorkingDirectory { get; set; } = "";
    public string DefaultMode { get; set; } = "medium";
    public string DefaultEffort { get; set; } = "";
    public string DefaultExecutor { get; set; } = "local";
    public string RunnerId { get; set; } = "";
    public bool ShowThinking { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public string Visibility { get; set; } = "workspace";

    /// <summary>Base64 of the DPAPI-protected API key; never the plain key.</summary>
    public string ProtectedApiKey { get; set; } = "";

    [JsonIgnore]
    public string ApiKey
    {
        get
        {
            if (string.IsNullOrEmpty(ProtectedApiKey)) return "";
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedApiKey), null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return ""; }
            catch (FormatException) { return ""; }
        }
        set => ProtectedApiKey = string.IsNullOrEmpty(value)
            ? ""
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine($"Settings load failed: {e.Message}");
        }
        return new AppSettings();
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext;

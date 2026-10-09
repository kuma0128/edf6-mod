using System.Globalization;
using System.Text.Json;

namespace EDF6WeaponUnlocker.Core;

public static class UiText
{
    public static string LanguageCode => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en";
    public static string T(string japanese, string english) => LanguageCode == "ja" ? japanese : english;
    public static void SetLanguage(string code)
    {
        if (code is not ("ja" or "en")) throw new ArgumentException("Language must be ja or en.", nameof(code));
        var culture = CultureInfo.GetCultureInfo(code == "ja" ? "ja-JP" : "en-US");
        CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = culture;
    }
}

public sealed class LanguagePreference
{
    private readonly string _path;
    public LanguagePreference(string appName, string? settingsDirectory = null) => _path = Path.Combine(
        settingsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName), "language.json");
    public string Load()
    {
        try
        {
            using var data = JsonDocument.Parse(File.ReadAllText(_path));
            var code = data.RootElement.GetProperty("language").GetString();
            if (code is "ja" or "en") return code;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en";
    }
    public void Save(string code)
    {
        if (code is not ("ja" or "en")) throw new ArgumentException("Language must be ja or en.", nameof(code));
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(new { language = code })); File.Move(temporary, _path, overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

using System.Globalization;
using System.Text;
using EDF6WeaponUnlocker.Core;
using static EDF6WeaponUnlocker.Core.UiText;

namespace EDF6LocationSync.Settings;

public sealed record SyncConfiguration(bool Enabled = true, int IntervalFrames = 30, string Language = "auto")
{
    public const string KnownSha256 = "0D5092393A1B9D8F1B75B0C93D8A8F65615A36A92C46E40349C2B66C733D0916";
    public const string RelativePath = "Mods/EDF6PositionSyncFix.ini";
    private const string Section = "PositionSyncFix";

    public static SyncConfiguration Load(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var active = false;
        foreach (var line in File.Exists(path) ? File.ReadAllLines(path) : [])
        {
            var text = line.Trim();
            if (text.StartsWith('[')) { active = text.Equals("[" + Section + "]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!active || text.StartsWith(';') || text.StartsWith('#')) continue;
            var index = text.IndexOf('=');
            if (index >= 0) values[text[..index].Trim()] = text[(index + 1)..].Trim();
        }
        var interval = int.TryParse(values.GetValueOrDefault("IntervalFrames"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? Math.Clamp(number, 10, 120) : 30;
        var language = values.GetValueOrDefault("Language")?.ToLowerInvariant();
        return new SyncConfiguration(values.GetValueOrDefault("Enabled") != "0", interval, language is "ja" or "en" ? language : "auto");
    }

    public void Save(string path)
    {
        if (Language is not ("auto" or "ja" or "en") || IntervalFrames is < 10 or > 120)
            throw new ArgumentException(T("設定値が範囲外です。", "Invalid configuration value."));
        var original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var lines = original is null ? [] : File.ReadAllLines(path).ToList();
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enabled"] = Enabled ? "1" : "0", ["IntervalFrames"] = IntervalFrames.ToString(CultureInfo.InvariantCulture), ["Language"] = Language,
        };
        var output = new List<string>(); var inSection = false; var found = false;
        void FinishSection()
        {
            if (!inSection) return;
            foreach (var (key, value) in expected) output.Add(key + "=" + value);
            expected.Clear();
        }
        foreach (var line in lines)
        {
            var text = line.Trim();
            if (text.StartsWith('['))
            {
                FinishSection();
                inSection = text.Equals("[" + Section + "]", StringComparison.OrdinalIgnoreCase);
                if (inSection) { if (found) throw new IOException(T("設定セクションが重複しています。INIを確認してください。", "Duplicate config section. Check the INI.")); found = true; }
                output.Add(line); continue;
            }
            var equals = text.IndexOf('=');
            if (inSection && equals >= 0 && new[] { "Enabled", "IntervalFrames", "Language" }.Contains(text[..equals].Trim(), StringComparer.OrdinalIgnoreCase)) continue;
            output.Add(line);
        }
        FinishSection();
        if (!found)
        {
            output.Add("[" + Section + "]");
            foreach (var (key, value) in expected) output.Add(key + "=" + value);
        }
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllLines(temp, output, Encoding.Unicode); // Win32 profile API supports UTF-16 BOM.
            if (original is null ? File.Exists(full) : !File.Exists(full) || !File.ReadAllBytes(full).AsSpan().SequenceEqual(original))
                throw new IOException(T("設定が別の処理で変更されました。再読み込みしてください。", "Settings changed externally. Reload them."));
            File.Move(temp, full, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

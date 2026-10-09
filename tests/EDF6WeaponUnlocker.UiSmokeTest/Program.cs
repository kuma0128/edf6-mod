using System.Reflection;
using EDF6WeaponUnlocker;
using EDF6WeaponUnlocker.Core;
using EDF6LocationSync.Settings;

namespace EDF6WeaponUnlocker.UiSmokeTest;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        var root = Path.Combine(Path.GetTempPath(), $"edf6-ui-{Guid.NewGuid():N}");
        var saveRoot = Path.Combine(root, "saves");
        var slot = Path.Combine(saveRoot, "test-account", "saveslot00");
        Directory.CreateDirectory(slot);
        var plain = new byte[Edf6SaveCodec.ExpectedMainSaveLength]; "MDB0"u8.CopyTo(plain);
        var encrypted = Edf6SaveCodec.Encode(plain, "MAIN.GST");
        File.WriteAllBytes(Path.Combine(slot, "MAIN.GST"), encrypted);
        File.WriteAllBytes(Path.Combine(slot, "DEFP_DLC1.MST"), [0]);
        var screenshots = args.FirstOrDefault();
        try
        {
            using (var form = new MainForm(saveRoot, Path.Combine(root, "weapon-prefs"), "ja"))
            {
                Invoke(form, "RefreshSlots", false);
                var lang = Find<ComboBox>(form, "LanguageSelector");
                lang.SelectedIndex = 1;
                var dlc2 = Find<CheckBox>(form, "Dlc2"); dlc2.Checked = true;
                Invoke(form, "RefreshSlots", false);
                Check(dlc2.Checked, "Manual DLC2 selection must survive refresh.");
                for (var i = 0; i < 2; i++)
                {
                    lang.SelectedIndex = i;
                    Check(Find<Button>(form, "UnlockButton").Enabled, "Unlock action must be available after discovering a save.");
                    Check(Find<Button>(form, "UnlockButton").Text == (i == 0 ? "全武器を★MAX" : "Unlock + MAX"), "Button translation");
                    Check(Find<ComboBox>(form, "SlotSelector").GetItemText(Find<ComboBox>(form, "SlotSelector").SelectedItem)?.Contains(i == 0 ? "検出" : "detected") == true, "Slot translation");
                    Check(dlc2.Checked, "Language switch must preserve DLC2 selection.");
                    // Preview completion is awaited while pumping this isolated form's synchronization context.
                    var preview = (Task)Invoke(form, "PreviewAsync", false)!;
                    var deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!preview.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
                    Check(preview.IsCompletedSuccessfully, "Preview did not finish.");
                    Check(Find<RichTextBox>(form, "Log").Text.Contains(i == 0 ? "SHA-256" : "SHA-256"), "Preview log missing hash.");
                    Render(form, screenshots, "weaponunlocker-" + (i == 0 ? "ja" : "en"));
                    Check(new LanguagePreference("test", Path.Combine(root, "weapon-prefs")).Load() == (i == 0 ? "ja" : "en"), "Language persistence");
                }
                var prefs = Path.Combine(root, "weapon-prefs");
                using var reopened = new MainForm(saveRoot, prefs);
                Check(reopened.Text == "EDF6 Weapon Unlocker", "Persisted language on relaunch");
                // Changing selection must clear stale preview counts.
                dlc2.Checked = false;
                Check(Find<Label>(form, "Status").Text.Length > 0, "Selection invalidation status");
                File.Delete(Path.Combine(slot, "MAIN.GST")); Invoke(form, "RefreshSlots", false);
                Check(!Find<Button>(form, "UnlockButton").Enabled, "No-save state must disable writing.");
                File.WriteAllBytes(Path.Combine(slot, "MAIN.GST"), encrypted);
            }
            using (var settings = new SettingsForm(root, Path.Combine(root, "sync-prefs"), "ja"))
            {
                var lang = Find<ComboBox>(settings, "LanguageSelector");
                var enabled = Find<CheckBox>(settings, "EnabledSetting");
                var interval = Find<NumericUpDown>(settings, "IntervalSetting");
                enabled.Checked = false; interval.Value = 60;
                for (var i = 0; i < 2; i++)
                {
                    lang.SelectedIndex = i;
                    Check(Find<Button>(settings, "SaveSettingsButton").Text == (i == 0 ? "設定を保存" : "Save settings"), "Settings translation");
                    Check(!enabled.Checked && interval.Value == 60, "Language switch must preserve settings.");
                    Render(settings, screenshots, "locationsync-" + (i == 0 ? "ja" : "en"));
                }
            }
            TestConfiguration(root);
            Check(File.ReadAllBytes(Path.Combine(slot, "MAIN.GST")).AsSpan().SequenceEqual(encrypted), "UI tests must never modify saves.");
            Console.WriteLine("PASS: bilingual forms, language persistence, previews, selection refresh, no-save state, layouts and INI round-trip.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            var full = Path.GetFullPath(root);
            var prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\') + "\\edf6-ui-";
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe test cleanup");
            Directory.Delete(full, true);
        }
    }
    private static void TestConfiguration(string root)
    {
        var path = Path.Combine(root, "config", "settings.ini");
        Check(SyncConfiguration.Load(path) == new SyncConfiguration(), "Missing INI defaults");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "; preserved comment\n[Unrelated]\nOther=keep\n[PositionSyncFix]\nIntervalFrames=999\nEnabled=0\nLanguage=en\nCustom=value\n");
        Check(SyncConfiguration.Load(path) == new SyncConfiguration(false, 120, "en"), "INI bounds and language");
        var config = new SyncConfiguration(true, 45, "ja"); config.Save(path);
        Check(SyncConfiguration.Load(path) == config, "INI round-trip");
        var contents = File.ReadAllText(path);
        Check(contents.Contains("Other=keep") && contents.Contains("Custom=value") && contents.Contains("; preserved comment"), "Unrelated settings must remain.");
        Check(File.ReadAllBytes(path).AsSpan(0, 2).SequenceEqual(new byte[] { 0xFF, 0xFE }), "Win32-compatible UTF-16");
        File.AppendAllText(path, "[PositionSyncFix]\nEnabled=0\n", System.Text.Encoding.Unicode);
        var threw = false; try { config.Save(path); } catch (IOException) { threw = true; }
        Check(threw, "Duplicate sections must be rejected.");
    }
    private static object? Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static T Find<T>(Control root, string name) where T : Control => Descendants(root).OfType<T>().Single(c => c.Name == name);
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static void Render(Form form, string? output, string name)
    {
        // Internal application rendering; no screen capture or browser/native UI input.
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000); form.Opacity = 0;
        form.Show();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (form is MainForm && !Find<Button>(form, "PreviewButton").Enabled && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        Application.DoEvents(); form.PerformLayout();
        foreach (var c in Descendants(form).Where(c => c is TextBox or Label or RichTextBox))
            c.Text = System.Text.RegularExpressions.Regex.Replace(c.Text, @"[A-Za-z]:\\Users\\[^\\]+\\AppData\\Local\\Temp\\edf6-ui-[a-f0-9]+", @"C:\Example");
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        foreach (var b in Descendants(form).OfType<Button>())
            Check(b.Width >= 75 && b.Height >= 22, "Collapsed button: " + b.Name);
        if (output is not null) { Directory.CreateDirectory(output); bitmap.Save(Path.Combine(output, name + ".png")); }
        form.Hide();
    }
}

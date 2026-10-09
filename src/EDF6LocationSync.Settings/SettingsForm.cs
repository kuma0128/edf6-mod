using System.Security.Cryptography;
using EDF6WeaponUnlocker.Core;
using static EDF6WeaponUnlocker.Core.UiText;

namespace EDF6LocationSync.Settings;

public sealed class SettingsForm : Form
{
    private readonly LanguagePreference _preference;
    private readonly List<Action> _translations = [];
    private readonly ComboBox _language = new() { Name = "LanguageSelector", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _folder = new() { Name = "GameFolder", Dock = DockStyle.Fill, ReadOnly = true };
    private readonly CheckBox _enabled = new() { Name = "EnabledSetting", AutoSize = true };
    private readonly NumericUpDown _interval = new() { Name = "IntervalSetting", Minimum = 10, Maximum = 120, Value = 30, Width = 110 };
    private readonly Label _status = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 190, 72) };
    private bool _loading;
    public SettingsForm(string? gameDirectory = null, string? settingsDirectory = null, string? language = null)
    {
        _preference = new LanguagePreference("EDF6LocationSync", settingsDirectory);
        SetLanguage(language ?? _preference.Load());
        ClientSize = new Size(900, 580); MinimumSize = new Size(860, 600);
        Font = new Font("Yu Gothic UI", 10); BackColor = Color.FromArgb(13, 20, 33); ForeColor = Color.White;
        StartPosition = FormStartPosition.CenterScreen;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 2, RowCount = 9 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        foreach (var h in new[] { 64, 34, 44, 45, 54, 106, 48 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);
        root.Controls.Add(new Label { AutoSize = true, Text = "EDF6  LOCATION SYNC", Font = new Font("Segoe UI", 22, FontStyle.Bold) }, 0, 0);
        _language.Items.AddRange(["日本語", "English"]); root.Controls.Add(_language, 1, 0);
        var folderCaption = Label("EDF6のインストールフォルダー", "EDF6 installation folder"); root.Controls.Add(folderCaption, 0, 1);
        root.Controls.Add(_folder, 0, 2);
        var browse = Button("参照…", "Browse…"); browse.Name = "BrowseButton"; root.Controls.Add(browse, 1, 2);
        browse.Click += (_, _) => { using var dialog = new FolderBrowserDialog { InitialDirectory = _folder.Text }; if (dialog.ShowDialog(this) == DialogResult.OK) LoadFolder(dialog.SelectedPath); };
        Bind(_enabled, "位置補正を有効にする（実験版）", "Enable position correction (experimental)");
        root.Controls.Add(_enabled, 0, 3); root.SetColumnSpan(_enabled, 2);
        var intervalRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        intervalRow.Controls.Add(Label("補正間隔（フレーム）", "Correction interval (frames)")); intervalRow.Controls.Add(_interval);
        intervalRow.Controls.Add(Label("10～120 / 標準30", "10–120 / default 30"));
        root.Controls.Add(intervalRow, 0, 4); root.SetColumnSpan(intervalRow, 2);
        var note = Label(
            "受信側のリモート兵士に対する補正を調整します。通信パケットは変更しません。\n間隔を短くすると位置の飛びが目立つ場合があります。\nオンライン実機検証は未実施です。ゲームを終了してから設定を保存してください。",
            "Adjusts receiver-side correction for remote soldiers. Does not change packets.\nShorter intervals may cause more visible snapping.\nLive online behavior is unverified. Close the game before saving settings.");
        note.AutoSize = false; note.Dock = DockStyle.Fill; root.Controls.Add(note, 0, 5); root.SetColumnSpan(note, 2);
        var save = Button("設定を保存", "Save settings"); save.Name = "SaveSettingsButton";
        root.Controls.Add(save, 0, 6); root.SetColumnSpan(save, 2);
        save.Click += (_, _) => SaveSettings();
        root.Controls.Add(_status, 0, 7); root.SetColumnSpan(_status, 2);
        root.Controls.Add(new Label { Text = "v0.2.0 · EDFModLoader required", AutoSize = true }, 0, 8);
        _language.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            SetLanguage(_language.SelectedIndex == 0 ? "ja" : "en"); Translate();
            try { _preference.Save(LanguageCode); } catch (Exception error) { ShowError(error); }
        };
        LoadFolder(gameDirectory ?? AppContext.BaseDirectory, language);
    }
    private void Bind(Control c, string ja, string en) => _translations.Add(() => c.Text = T(ja, en));
    private Label Label(string ja, string en) { var l = new Label { AutoSize = true, Margin = new Padding(3, 5, 12, 3) }; Bind(l, ja, en); return l; }
    private Button Button(string ja, string en)
    {
        var b = new Button { Dock = DockStyle.Fill, BackColor = Color.FromArgb(62, 166, 255), ForeColor = Color.FromArgb(13, 20, 33), FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0; Bind(b, ja, en); return b;
    }
    private void Translate()
    {
        _loading = true;
        Text = T("EDF6 LocationSync 設定", "EDF6 LocationSync Settings");
        foreach (var t in _translations) t();
        _language.SelectedIndex = LanguageCode == "ja" ? 0 : 1;
        _status.Text = T("ゲーム内に言語メニューは追加しません。言語・補正設定はゲームの次回起動時に反映されます。", "No in-game language menu is added. Saved language and correction settings apply on the next game launch.");
        _loading = false;
    }
    private void LoadFolder(string folder, string? language = null)
    {
        try
        {
            var config = SyncConfiguration.Load(Path.Combine(folder, SyncConfiguration.RelativePath));
            _folder.Text = Path.GetFullPath(folder); _enabled.Checked = config.Enabled; _interval.Value = config.IntervalFrames;
            if (language is not null) SetLanguage(language);
            else if (config.Language is "ja" or "en") SetLanguage(config.Language);
            Translate();
        }
        catch (Exception error) { Translate(); ShowError(error); }
    }
    private void SaveSettings()
    {
        try
        {
            SaveDiscovery.EnsureGameIsClosed();
            var dll = Path.Combine(_folder.Text, "EDF.dll");
            if (!File.Exists(Path.Combine(_folder.Text, "EDF6.exe")) || !File.Exists(dll))
                throw new IOException(T("EDF6.exeとEDF.dllがあるフォルダーを選択してください。", "Select the folder containing EDF6.exe and EDF.dll."));
            using (var file = File.OpenRead(dll))
            {
                if (Convert.ToHexString(SHA256.HashData(file)) != SyncConfiguration.KnownSha256 && _enabled.Checked)
                    throw new IOException(T("このEDF.dllは未対応です。有効化せずに中止しました。", "This EDF.dll is unsupported. Stopped without enabling the mod."));
            }
            new SyncConfiguration(_enabled.Checked, (int)_interval.Value, LanguageCode).Save(Path.Combine(_folder.Text, SyncConfiguration.RelativePath));
            _status.Text = T("設定を保存しました。ゲームの次回起動時に反映されます。", "Settings saved. Changes apply on the next game launch.");
        }
        catch (Exception error) { ShowError(error); }
    }
    private void ShowError(Exception error) => MessageBox.Show(this, error.Message, T("設定エラー", "Settings error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
}

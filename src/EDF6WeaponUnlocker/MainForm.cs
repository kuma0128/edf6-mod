using System.Diagnostics;
using EDF6WeaponUnlocker.Core;
using static EDF6WeaponUnlocker.Core.UiText;

namespace EDF6WeaponUnlocker;

public sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 20, 33);
    private static readonly Color Surface = Color.FromArgb(23, 34, 52);
    private static readonly Color Primary = Color.FromArgb(62, 166, 255);
    private static readonly Color Success = Color.FromArgb(61, 214, 140);
    private static readonly Color Warning = Color.FromArgb(255, 190, 72);
    private static readonly Color TextPrimary = Color.FromArgb(242, 247, 255);
    private static readonly Color TextMuted = Color.FromArgb(166, 181, 203);

    private readonly UnlockService _service = new();
    private readonly string? _saveRoot;
    private readonly LanguagePreference _preference;
    private readonly List<Action> _translations = [];
    private readonly List<(DateTime Time, Func<string> Text)> _messages = [];
    private readonly ComboBox _language = new() { Name = "LanguageSelector", DropDownStyle = ComboBoxStyle.DropDownList, Width = 136 };
    private readonly ComboBox _slots = new() { Name = "SlotSelector", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _refresh = new() { Name = "RefreshButton" };
    private readonly Label _path = new() { AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = TextMuted };
    private readonly CheckBox _dlc1 = new() { Name = "Dlc1", AutoSize = true };
    private readonly CheckBox _dlc2 = new() { Name = "Dlc2", AutoSize = true };
    private readonly Label[] _metrics = Enumerable.Range(0, 4).Select(_ => new Label { Text = "—", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 21, FontStyle.Bold) }).ToArray();
    private readonly Button _preview = new() { Name = "PreviewButton" };
    private readonly Button _unlock = new() { Name = "UnlockButton" };
    private readonly Button _restore = new() { Name = "RestoreButton" };
    private readonly Button _backups = new() { Name = "BackupFolderButton" };
    private readonly RichTextBox _log = new() { Name = "Log", Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Surface, ForeColor = TextMuted };
    private readonly Label _statusLabel = new() { Name = "Status", Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly ToolTip _toolTip = new();
    private Func<string> _status = () => T("準備中", "Preparing");
    private UnlockPreview? _lastPreview;
    private bool _busy, _changingLanguage;

    public MainForm(string? saveRoot = null, string? settingsDirectory = null, string? language = null)
    {
        _saveRoot = saveRoot;
        _preference = new LanguagePreference("EDF6WeaponUnlocker", settingsDirectory);
        SetLanguage(language ?? _preference.Load());
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1020, 800);
        MinimumSize = new Size(980, 790);
        BackColor = Background; ForeColor = TextPrimary;
        Font = new Font("Yu Gothic UI", 10f);
        Icon = SystemIcons.Shield;
        BuildInterface();
        ApplyLanguage();
        Shown += (_, _) => RefreshSlots(true);
        FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
    }

    private SaveSlotInfo? SelectedSlot => _slots.SelectedItem as SaveSlotInfo;
    private UnlockOptions SelectedOptions => new(SelectedSlot?.HasDlc1 == true && _dlc1.Checked, _dlc2.Checked, SelectedSlot?.HasDlc2 != true && _dlc2.Checked);
    private void Bind(Control c, string ja, string en) => _translations.Add(() => c.Text = T(ja, en));
    private Label Caption(string ja, string en)
    {
        var label = new Label { AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 4, 0, 8) };
        Bind(label, ja, en); return label;
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24) };
        foreach (var h in new[] { 86, 180, 112, 60 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); Controls.Add(root);
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        header.Controls.Add(new Label { Text = "EDF6  WEAPON UNLOCKER", AutoSize = true, Font = new Font("Segoe UI", 21, FontStyle.Bold) }, 0, 0);
        _language.Items.AddRange(["日本語", "English"]);
        _language.SelectedIndex = LanguageCode == "ja" ? 0 : 1;
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
        header.Controls.Add(_language, 1, 0);
        var subtitle = Caption("全兵科の武器取得・★MAX / 検証と自動バックアップ", "All classes · Weapon unlock + maximum stars · Verified backups");
        header.Controls.Add(subtitle, 0, 1); header.SetColumnSpan(subtitle, 2); root.Controls.Add(header, 0, 0);

        var save = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(16, 10, 16, 8), ColumnCount = 2, RowCount = 5 };
        save.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        save.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        var saveCaption = Caption("対象セーブ", "Save slot");
        save.Controls.Add(saveCaption, 0, 0); save.SetColumnSpan(saveCaption, 2);
        _slots.SelectedIndexChanged += (_, _) => OnSlotChanged();
        _slots.FormattingEnabled = true;
        _slots.Format += (_, e) => { if (e.ListItem is SaveSlotInfo slot) e.Value = slot.ToString(); };
        save.Controls.Add(_slots, 0, 1);
        StyleButton(_refresh, "再検出", "Refresh", Primary);
        _refresh.Click += (_, _) => RefreshSlots(true); save.Controls.Add(_refresh, 1, 1);
        save.Controls.Add(_path, 0, 2); save.SetColumnSpan(_path, 2);
        var dlc = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        _dlc1.Margin = new Padding(0, 0, 28, 0);
        _dlc1.CheckedChanged += (_, _) => InvalidatePreview();
        _dlc2.CheckedChanged += (_, _) => InvalidatePreview();
        dlc.Controls.AddRange([_dlc1, _dlc2]); save.Controls.Add(dlc, 0, 3); save.SetColumnSpan(dlc, 2);
        var note = Caption("所持済みの特典武器は★MAXにします。未所持の別売り・特典は開放しません。", "Owned bonus weapons are maxed. Unowned paid/bonus items stay locked.");
        note.ForeColor = Warning; save.Controls.Add(note, 0, 4); save.SetColumnSpan(note, 2);
        foreach (var h in new[] { 26, 36, 26, 28 }) save.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        save.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.Controls.Add(save, 0, 1);

        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 12, 0, 8) };
        string[] ja = ["選択対象", "新規取得", "★MAX化", "変更不要"], en = ["Selected", "New unlocks", "Upgrade to MAX", "Already maxed"];
        for (var i = 0; i < 4; i++)
        {
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, RowCount = 2, Margin = new Padding(0, 0, 8, 0) };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var c = Caption(ja[i], en[i]); c.Dock = DockStyle.Fill; c.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(c, 0, 0); card.Controls.Add(_metrics[i], 0, 1); metrics.Controls.Add(card, i, 0);
        }
        root.Controls.Add(metrics, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Padding = new Padding(0, 4, 0, 8) };
        StyleButton(_preview, "安全チェック", "Inspect save", Primary);
        StyleButton(_unlock, "全武器を★MAX", "Unlock + MAX", Success);
        StyleButton(_restore, "直前へ復元", "Restore latest", Surface);
        StyleButton(_backups, "バックアップ", "Backups", Surface);
        _preview.Click += async (_, _) => await PreviewAsync();
        _unlock.Click += async (_, _) => await UnlockAsync();
        _restore.Click += async (_, _) => await RestoreAsync();
        _backups.Click += (_, _) => OpenBackupFolder();
        Button[] buttons = [_preview, _unlock, _restore, _backups];
        for (var i = 0; i < buttons.Length; i++) { actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); actions.Controls.Add(buttons[i], i, 0); }
        root.Controls.Add(actions, 0, 3);
        var logPanel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(16) };
        logPanel.Controls.Add(_log); root.Controls.Add(logPanel, 0, 4);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        footer.Controls.Add(_statusLabel, 0, 0); footer.Controls.Add(new Label { Text = "v1.3.0", AutoSize = true, ForeColor = TextMuted }, 1, 0);
        root.Controls.Add(footer, 0, 5); SetActionsEnabled(false);
    }

    private void StyleButton(Button c, string ja, string en, Color color)
    {
        Bind(c, ja, en); c.Dock = DockStyle.Fill; c.BackColor = color;
        c.ForeColor = color == Surface ? TextPrimary : Background;
        c.FlatStyle = FlatStyle.Flat; c.FlatAppearance.BorderSize = 0;
        c.Margin = new Padding(3, 0, 8, 0); c.Font = new Font(Font, FontStyle.Bold);
    }
    private void ChangeLanguage()
    {
        if (_changingLanguage || _busy) return;
        SetLanguage(_language.SelectedIndex == 0 ? "ja" : "en"); ApplyLanguage();
        try { _preference.Save(LanguageCode); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { ShowError(e); }
    }
    private void ApplyLanguage()
    {
        _changingLanguage = true; SuspendLayout();
        Text = T("EDF6 武器全開放ツール", "EDF6 Weapon Unlocker");
        foreach (var translate in _translations) translate();
        _language.SelectedIndex = LanguageCode == "ja" ? 0 : 1; UpdateDlcLabels();
        // The ComboBox caches formatted text: reassigning the same item refreshes it.
        for (var i = 0; i < _slots.Items.Count; i++) _slots.Items[i] = _slots.Items[i];
        _statusLabel.Text = _status(); RenderLog(); ResumeLayout(true); _changingLanguage = false;
    }
    private void UpdateDlcLabels()
    {
        _dlc1.Text = SelectedSlot?.HasDlc1 == true ? T("DLC1武器（検出済み）", "DLC1 weapons (detected)") : T("DLC1未検出", "DLC1 not detected");
        _dlc2.Text = SelectedSlot?.HasDlc2 == true ? T("DLC2武器（検出済み）", "DLC2 weapons (detected)") : T("DLC2武器（手動指定）", "DLC2 weapons (manual)");
        _toolTip.SetToolTip(_dlc2, T("DLC2所持者向け。ミッション未検出でも武器110件を対象にできます。", "For DLC2 owners. Includes 110 weapons even if the mission save is missing."));
    }
    private void RefreshSlots(bool runPreview)
    {
        if (_busy) return;
        try
        {
            var previousPath = SelectedSlot?.DirectoryPath; var options = SelectedOptions;
            _lastPreview = null; _slots.Items.Clear();
            var slots = SaveDiscovery.Discover(_saveRoot);
            foreach (var slot in slots) _slots.Items.Add(slot);
            if (slots.Count == 0)
            {
                InvalidatePreview(); _dlc1.Checked = _dlc2.Checked = false; _path.Text = _saveRoot ?? SaveDiscovery.DefaultSaveRoot;
                Log(() => T("MAIN.GSTが見つかりません。EDF6でセーブを作成してから再検出してください。", "No MAIN.GST found. Create a save in EDF6, then refresh."), true);
                SetStatus(() => T("セーブ未検出", "No save found"), Warning); UpdateDlcLabels(); SetActionsEnabled(false); return;
            }
            _slots.SelectedItem = slots.FirstOrDefault(s => string.Equals(s.DirectoryPath, previousPath, StringComparison.OrdinalIgnoreCase)) ?? slots[0];
            if (SelectedSlot is { } selected && selected.DirectoryPath == previousPath) { _dlc1.Checked = selected.HasDlc1 && options.IncludeDlc1; _dlc2.Checked = options.IncludeDlc2; }
            SetActionsEnabled(true);
            Log(() => T($"{slots.Count}個のセーブスロットを検出しました。", $"Found {slots.Count} save slot(s)."), true);
            if (runPreview) _ = PreviewAsync();
        }
        catch (Exception e) { InvalidatePreview(); ShowError(e); }
    }
    private void OnSlotChanged()
    {
        if (_changingLanguage) return;
        InvalidatePreview();
        if (SelectedSlot is not { } slot) return;
        _path.Text = slot.MainSavePath; _toolTip.SetToolTip(_path, slot.MainSavePath);
        _dlc1.Checked = slot.HasDlc1; _dlc2.Checked = slot.HasDlc2;
        UpdateDlcLabels(); SetActionsEnabled(true);
    }
    private async Task PreviewAsync(bool preserveLog = false)
    {
        if (SelectedSlot is not { } slot || _busy) return;
        var options = SelectedOptions; _lastPreview = null;
        await RunBusyAsync(() => T("セーブを復号・検証中…", "Decrypting and checking save…"), async () =>
        {
            var p = await Task.Run(() => _service.Inspect(slot, options)); _lastPreview = p;
            int[] values = [p.SelectedWeapons, p.NewlyUnlocked, p.WeaponsToMax, p.AlreadyMaxed];
            for (var i = 0; i < values.Length; i++) _metrics[i].Text = values[i].ToString("N0");
            Log(() => PreviewText(p), !preserveLog);
            SetStatus(() => p.WeaponsToMax == 0 ? T("全対象武器は取得済み・★MAXです", "All selected weapons are unlocked and maxed") : T("安全チェックOK — EDF6終了後に書き込めます", "Save verified — close EDF6 before applying"), Success);
        });
    }
    private async Task UnlockAsync()
    {
        if (SelectedSlot is not { } slot || _busy) return;
        // Refresh before confirmation; Apply rechecks the hash at commit time.
        await PreviewAsync();
        if (_lastPreview is not { } p || p.SavePath != slot.MainSavePath) return;
        if (p.WeaponsToMax == 0) { MessageBox.Show(this, T("すべて取得済み・★MAXです。", "All selected weapons are already unlocked and maxed."), Text); return; }
        try { SaveDiscovery.EnsureGameIsClosed(); } catch (Exception e) { ShowError(e); return; }
        var warning = p.Options.AllowDlc2WithoutMissionSave ? T("\nDLC2手動指定: DLC2を所持・導入済みの場合だけ続行してください。\n", "\nManual DLC2: continue only if you own and have installed DLC2.\n") : "";
        var message = T(
            $"{slot.SlotName}: {p.WeaponsToMax}件を★MAX化（新規取得 {p.NewlyUnlocked}件）。\n非表示修復: {p.VisibilityRepairs}件\n\n武器別の正規上限へ更新し、所持フラグと既存の特殊値を保持します。原本をバックアップします。未所持の特典武器は開放しません。\n{warning}\n続行しますか？",
            $"{slot.SlotName}: maximize {p.WeaponsToMax} weapons ({p.NewlyUnlocked} new unlocks).\nVisibility repairs: {p.VisibilityRepairs}\n\nUse each weapon's normal star caps, preserving ownership flags and existing special values. The original is backed up first. Unowned bonus weapons stay locked.\n{warning}\nContinue?");
        if (MessageBox.Show(this, message, T("最終確認", "Confirm changes"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var completed = await RunBusyAsync(() => T("バックアップ・書き込み・再検証中…", "Backing up, applying and verifying…"), async () =>
        {
            var r = await Task.Run(() => _service.Apply(slot, p.Options, p.SourceSha256)); _lastPreview = null;
            Log(() => T($"完了: {r.Preview.NewlyUnlocked}件を新規取得、{r.Preview.WeaponsToMax}件を★MAX化。\nバックアップ: {r.BackupDirectory}\n変更バイト数: {r.ChangedByteCount}\nSHA-256: {r.ModifiedSha256}", $"Complete: {r.Preview.NewlyUnlocked} new unlocks, {r.Preview.WeaponsToMax} weapons maxed.\nBackup: {r.BackupDirectory}\nChanged bytes: {r.ChangedByteCount}\nSHA-256: {r.ModifiedSha256}"), true);
        });
        if (completed) await PreviewAsync(true);
    }
    private async Task RestoreAsync()
    {
        if (SelectedSlot is not { } slot || _busy) return;
        BackupInfo? backup;
        try { backup = _service.GetBackups(slot).FirstOrDefault(); } catch (Exception e) { ShowError(e); return; }
        if (backup is null) { MessageBox.Show(this, T("このスロットのバックアップはありません。", "No backups were found for this slot."), Text); return; }
        var message = T($"{backup.CreatedAt:yyyy/MM/dd HH:mm:ss} のバックアップへ復元します。\n現在のセーブも復元前に保存します。", $"Restore the backup from {backup.CreatedAt:yyyy/MM/dd HH:mm:ss}?\nThe current save will also be backed up first.");
        if (MessageBox.Show(this, message, T("復元の確認", "Confirm restore"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        var completed = await RunBusyAsync(() => T("バックアップを検証・復元中…", "Verifying and restoring backup…"), async () =>
        {
            var r = await Task.Run(() => _service.Restore(slot, backup)); _lastPreview = null;
            Log(() => T($"復元完了: {r.RestoredFromDirectory}\n復元前の安全バックアップ: {r.SafetyBackupDirectory}\nSHA-256: {r.RestoredSha256}", $"Restored: {r.RestoredFromDirectory}\nSafety backup: {r.SafetyBackupDirectory}\nSHA-256: {r.RestoredSha256}"), true);
        });
        if (completed) await PreviewAsync(true);
    }
    private void OpenBackupFolder()
    {
        try { Directory.CreateDirectory(_service.BackupRoot); Process.Start(new ProcessStartInfo(_service.BackupRoot) { UseShellExecute = true }); }
        catch (Exception e) { ShowError(e); }
    }
    private static string PreviewText(UnlockPreview p) => T(
        $"安全チェック: OK\n対象: 本編{(p.Options.IncludeDlc1 ? " + DLC1" : "")}{(p.Options.IncludeDlc2 ? " + DLC2" : "")}\n新規取得: {p.NewlyUnlocked} / ★MAX化: {p.WeaponsToMax} / 非表示修復: {p.VisibilityRepairs}\n既に★MAX: {p.AlreadyMaxed} / 所持済み: {p.AlreadyOwned}\n予約スロット: {p.ProtectedSlotsSkipped}（変更なし）\n所持済み特典: {p.OwnedEntitlementItemsIncluded} / 未所持特典: {p.EntitlementItemsSkipped}（変更なし）\n未選択DLC: {p.UnavailableExpansionWeaponsSkipped}\nDLC2手動指定: {(p.Options.AllowDlc2WithoutMissionSave ? "あり（所持者向け）" : "なし")}\nCRC32C: 0x{p.StoredChecksum:X8} = 0x{p.ComputedChecksum:X8}\nSHA-256: {p.SourceSha256}",
        $"Save verification: OK\nScope: Base game{(p.Options.IncludeDlc1 ? " + DLC1" : "")}{(p.Options.IncludeDlc2 ? " + DLC2" : "")}\nNew unlocks: {p.NewlyUnlocked} / To maximize: {p.WeaponsToMax} / Visibility repairs: {p.VisibilityRepairs}\nAlready maxed: {p.AlreadyMaxed} / Already owned: {p.AlreadyOwned}\nReserved slots: {p.ProtectedSlotsSkipped} (unchanged)\nOwned bonus: {p.OwnedEntitlementItemsIncluded} / Unowned bonus: {p.EntitlementItemsSkipped} (unchanged)\nExcluded DLC weapons: {p.UnavailableExpansionWeaponsSkipped}\nManual DLC2: {(p.Options.AllowDlc2WithoutMissionSave ? "yes (owners only)" : "no")}\nCRC32C: 0x{p.StoredChecksum:X8} = 0x{p.ComputedChecksum:X8}\nSHA-256: {p.SourceSha256}");
    private async Task<bool> RunBusyAsync(Func<string> status, Func<Task> operation)
    {
        if (_busy) return false;
        _busy = true; UseWaitCursor = true; SetActionsEnabled(false); SetStatus(status, Primary);
        try { await operation(); return true; }
        catch (Exception e) { InvalidatePreview(); ShowError(e); return false; }
        finally { _busy = false; UseWaitCursor = false; SetActionsEnabled(SelectedSlot is not null); }
    }
    private void InvalidatePreview()
    {
        _lastPreview = null; foreach (var metric in _metrics) metric.Text = "—";
        SetStatus(() => T("★MAXボタンで安全チェックを自動実行します", "Unlock + MAX checks the save before applying"), TextMuted);
    }
    private void SetActionsEnabled(bool enabled)
    {
        _slots.Enabled = enabled && !_busy;
        _refresh.Enabled = _language.Enabled = _backups.Enabled = !_busy;
        _preview.Enabled = _restore.Enabled = _unlock.Enabled = enabled && !_busy;
        _dlc1.Enabled = enabled && !_busy && SelectedSlot?.HasDlc1 == true; _dlc2.Enabled = enabled && !_busy;
    }
    private void SetStatus(Func<string> text, Color color) { _status = text; _statusLabel.Text = text(); _statusLabel.ForeColor = color; }
    private void Log(Func<string> text, bool clear) { if (clear) _messages.Clear(); _messages.Add((DateTime.Now, text)); RenderLog(); }
    private void RenderLog() { _log.Text = string.Join(Environment.NewLine, _messages.Select(m => $"[{m.Time:HH:mm:ss}] {m.Text()}")); _log.SelectionStart = _log.TextLength; _log.ScrollToCaret(); }
    private void ShowError(Exception error)
    {
        Log(() => T($"エラー: {error.Message}", $"Error: {error.Message}"), false);
        SetStatus(() => T("処理を中止しました — エラーとバックアップを確認してください", "Operation stopped — review the error and backup"), Warning);
        MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    protected override void Dispose(bool disposing) { if (disposing) _toolTip.Dispose(); base.Dispose(disposing); }
}

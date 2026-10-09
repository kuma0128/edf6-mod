using System.Text.Encodings.Web;
using System.Text.Json;
using EDF6WeaponUnlocker.Core;
using static EDF6WeaponUnlocker.Core.UiText;

Console.OutputEncoding = System.Text.Encoding.UTF8;
SetLanguage(System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en");
return Run(args);

static int Run(string[] args)
{
    try
    {
        var options = Parse(args, out var command);
        string? Option(string name) => options.GetValueOrDefault(name);
        bool Has(string name) => options.ContainsKey(name);
        if (Option("--lang") is { } lang) SetLanguage(lang.ToLowerInvariant());
        if (command == "help")
        {
            Console.WriteLine("EDF6 Weapon Unlocker CLI 1.3.0");
            Console.WriteLine("  scan | inspect | unlock | backups | restore-latest | help");
            Console.WriteLine("  --lang ja|en --slot <folder> --backup-root <folder>");
            Console.WriteLine("  --include-dlc2 --no-dlc1 --no-dlc2 --yes --json");
            Console.WriteLine(T("DLC2手動指定は所有者のみ使用してください。ゲームを終了し、Steam Cloudの同期完了後に操作してください。",
                "Use the DLC2 override only if you own DLC2. Close the game and finish Steam Cloud sync before editing."));
            return 0;
        }
        var json = Has("--json");
        if (command == "scan")
        {
            var slots = SaveDiscovery.Discover();
            if (json) WriteJson(slots);
            else foreach (var item in slots) Console.WriteLine($"{item.SteamId} / {item}\n  {item.MainSavePath}");
            if (slots.Count == 0 && !json) Console.WriteLine(T("セーブが見つかりません。", "No save found."));
            return slots.Count == 0 ? 2 : 0;
        }
        var slot = ResolveSlot(Option("--slot"));
        var service = new UnlockService(Option("--backup-root"));
        if (command == "backups")
        {
            var backups = service.GetBackups(slot);
            if (json) WriteJson(backups); else foreach (var b in backups) Console.WriteLine($"{b}\n  {b.DirectoryPath}");
            if (backups.Count == 0 && !json) Console.WriteLine(T("このスロットのバックアップはありません。", "No backups for this slot."));
            return 0;
        }
        if (command == "restore-latest")
        {
            var backup = service.GetBackups(slot).FirstOrDefault()
                ?? throw new InvalidOperationException(T("このスロットのバックアップはありません。", "No backups for this slot."));
            if (!Has("--yes"))
            {
                Console.WriteLine(T($"復元対象: {backup}", $"Restore from: {backup}"));
                if (!Confirm("RESTORE")) return 3;
            }
            var result = service.Restore(slot, backup);
            if (json) WriteJson(result);
            else Console.WriteLine(T($"復元しました。復元前のバックアップ: {result.SafetyBackupDirectory}",
                $"Restored. Pre-restore backup: {result.SafetyBackupDirectory}"));
            return 0;
        }
        if (Has("--include-dlc2") && Has("--no-dlc2"))
            throw new ArgumentException(T("--include-dlc2と--no-dlc2は同時に指定できません。", "--include-dlc2 and --no-dlc2 cannot be combined."));
        var selection = new UnlockOptions(slot.HasDlc1 && !Has("--no-dlc1"),
            Has("--include-dlc2") || (slot.HasDlc2 && !Has("--no-dlc2")), Has("--include-dlc2") && !slot.HasDlc2);
        var preview = service.Inspect(slot, selection);
        if (command == "inspect")
        {
            if (json) WriteJson(preview); else PrintPreview(preview);
            return 0;
        }
        if (!Has("--yes")) { PrintPreview(preview); if (!Confirm("UNLOCK")) return 3; }
        var applied = service.Apply(slot, selection, preview.SourceSha256);
        if (json) WriteJson(applied);
        else Console.WriteLine(T(
            $"完了: 新規取得 {applied.Preview.NewlyUnlocked}件 / ★MAX {applied.Preview.WeaponsToMax}件\nバックアップ: {applied.BackupDirectory}",
            $"Complete: {applied.Preview.NewlyUnlocked} newly unlocked / {applied.Preview.WeaponsToMax} maxed\nBackup: {applied.BackupDirectory}"));
        return 0;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(T($"エラー: {error.Message}", $"Error: {error.Message}"));
        return 1;
    }
}

static Dictionary<string, string?> Parse(string[] args, out string command)
{
    command = "help";
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    string[] valued = ["--lang", "--slot", "--backup-root"];
    string[] flags = ["--yes", "--json", "--no-dlc1", "--no-dlc2", "--include-dlc2"];
    string[] commands = ["scan", "inspect", "unlock", "backups", "restore-latest", "help"];
    var foundCommand = false;
    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i].ToLowerInvariant();
        if (key is "-h" or "--help") key = "help";
        if (commands.Contains(key))
        {
            if (foundCommand) throw new ArgumentException(T("コマンドは1つだけ指定してください。", "Specify only one command."));
            command = key; foundCommand = true; continue;
        }
        if (result.ContainsKey(key)) throw new ArgumentException(T($"重複オプション: {key}", $"Duplicate option: {key}"));
        if (valued.Contains(key))
        {
            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(T($"{key}には値が必要です。", $"{key} requires a value."));
            result.Add(key, args[++i]);
            if (key == "--lang") SetLanguage(args[i].ToLowerInvariant());
        }
        else if (flags.Contains(key)) result.Add(key, null);
        else throw new ArgumentException(T($"不明な引数: {args[i]}", $"Unknown argument: {args[i]}"));
    }
    return result;
}

static bool Confirm(string token)
{
    Console.Write(T($"実行するには {token} と入力してください: ", $"Type {token} to continue: "));
    if (Console.ReadLine() == token) return true;
    Console.WriteLine(T("キャンセルしました。", "Cancelled."));
    return false;
}
static SaveSlotInfo ResolveSlot(string? requested)
{
    if (requested is not null)
    {
        var full = Path.GetFullPath(requested);
        var dir = Path.GetFileName(full).Equals("MAIN.GST", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(full)! : full;
        var main = Path.Combine(dir, "MAIN.GST");
        if (!File.Exists(main)) throw new FileNotFoundException(T("MAIN.GSTが見つかりません。", "MAIN.GST was not found."), main);
        return new SaveSlotInfo(Directory.GetParent(dir)?.Name ?? "manual", Path.GetFileName(dir), dir, main,
            File.GetLastWriteTime(main), File.Exists(Path.Combine(dir, "DEFP_DLC1.MST")), File.Exists(Path.Combine(dir, "DEFP_DLC2.MST")));
    }
    var slots = SaveDiscovery.Discover();
    return slots.Count switch
    {
        0 => throw new InvalidOperationException(T("有効なセーブスロットが見つかりません。", "No save slot found.")),
        1 => slots[0],
        _ => throw new InvalidOperationException(T("複数のセーブがあります。--slot <フォルダー>を指定してください。", "Multiple saves found. Specify --slot <folder>.")),
    };
}
static void PrintPreview(UnlockPreview p)
{
    Console.WriteLine(T($"対象: {p.SavePath}", $"Save: {p.SavePath}"));
    Console.WriteLine(T($"選択 {p.SelectedWeapons} / 新規取得 {p.NewlyUnlocked} / ★MAX化 {p.WeaponsToMax} / 表示修復 {p.VisibilityRepairs}",
        $"Selected {p.SelectedWeapons} / New {p.NewlyUnlocked} / To max {p.WeaponsToMax} / Visibility repairs {p.VisibilityRepairs}"));
    Console.WriteLine(T($"既に★MAX {p.AlreadyMaxed} / 既に所持 {p.AlreadyOwned}", $"Already maxed {p.AlreadyMaxed} / Already owned {p.AlreadyOwned}"));
    Console.WriteLine(T($"変更しない: 予約 {p.ProtectedSlotsSkipped} / 未所持特典 {p.EntitlementItemsSkipped} / 未選択DLC {p.UnavailableExpansionWeaponsSkipped}",
        $"Skipped: reserved {p.ProtectedSlotsSkipped} / unowned bonuses {p.EntitlementItemsSkipped} / unselected DLC {p.UnavailableExpansionWeaponsSkipped}"));
    Console.WriteLine(T($"所持済み特典の★MAX対象: {p.OwnedEntitlementItemsIncluded}", $"Owned bonus items included: {p.OwnedEntitlementItemsIncluded}"));
    if (p.Options.AllowDlc2WithoutMissionSave)
        Console.WriteLine(T("DLC2は手動指定です。所有者のみ使用してください。ミッションやSteamライセンスは変更しません。",
            "DLC2 override selected. Use only if you own DLC2. Mission data and Steam licenses are not changed."));
    Console.WriteLine($"CRC32C: 0x{p.StoredChecksum:X8} (OK)\nSHA-256: {p.SourceSha256}");
}
static void WriteJson<TValue>(TValue value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
{
    WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}));

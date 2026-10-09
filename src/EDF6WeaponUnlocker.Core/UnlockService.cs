using System.Security.Cryptography;
using System.Text.Json;

namespace EDF6WeaponUnlocker.Core;

public sealed class UnlockService
{
    private const string ToolName = "EDF6 Weapon Unlocker";
    private const string ToolVersion = "1.3.0";
    private const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _backupRoot;
    private readonly Action _ensureGameIsClosed;

    public UnlockService(string? backupRoot = null)
        : this(backupRoot, SaveDiscovery.EnsureGameIsClosed)
    {
    }

    internal UnlockService(string? backupRoot, Action ensureGameIsClosed)
    {
        _backupRoot = backupRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EDF6WeaponUnlocker",
            "Backups");
        _ensureGameIsClosed = ensureGameIsClosed;
    }

    public string BackupRoot => _backupRoot;

    public UnlockPreview Inspect(SaveSlotInfo slot, UnlockOptions options)
    {
        ValidateOptions(slot, options);
        var encrypted = ReadStableFile(slot.MainSavePath);
        return InspectBytes(slot, options, encrypted);
    }

    public UnlockResult Apply(
        SaveSlotInfo slot,
        UnlockOptions options,
        string? expectedSourceSha256 = null)
    {
        _ensureGameIsClosed();
        ValidateOptions(slot, options);

        var encrypted = ReadStableFile(slot.MainSavePath);
        var sourceHash = Sha256(encrypted);
        if (!string.IsNullOrWhiteSpace(expectedSourceSha256) &&
            !string.Equals(sourceHash, expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                UiText.T("プレビュー後にMAIN.GSTが更新されました。もう一度プレビューしてください。", "MAIN.GST changed after preview. Preview again before applying the unlock."));
        }

        var preview = InspectBytes(slot, options, encrypted);
        if (preview.WeaponsToMax == 0)
        {
            throw new InvalidOperationException(UiText.T("選択した武器はすべて取得済み・★MAXです。", "All selected weapons are already unlocked at their maximum stars."));
        }

        var decoded = Edf6SaveCodec.Decode(encrypted, Path.GetFileName(slot.MainSavePath));
        var originalPlain = decoded.Plaintext;
        var modifiedPlain = originalPlain.ToArray();
        var changedWeaponIds = new HashSet<int>();

        for (var weaponId = 0; weaponId < WeaponCatalog.KnownWeaponCount; weaponId++)
        {
            var record = Edf6SaveCodec.ReadWeapon(modifiedPlain, weaponId);
            var entitlementIsOwnedOrRepairable = record.IsOwned || record.NeedsVisibilityRepair;
            if (!WeaponCatalog.IsSelected(weaponId, options, entitlementIsOwnedOrRepairable) ||
                record.IsMaxed(weaponId))
            {
                continue;
            }

            Edf6SaveCodec.SetWeaponToMax(modifiedPlain, weaponId);
            changedWeaponIds.Add(weaponId);
        }

        Edf6SaveCodec.UpdateChecksum(modifiedPlain);
        ValidateOnlyExpectedBytesChanged(originalPlain, modifiedPlain, changedWeaponIds);
        var changedByteCount = CountWeaponTableChanges(originalPlain, modifiedPlain);

        var modifiedEncrypted = Edf6SaveCodec.Encode(modifiedPlain, Path.GetFileName(slot.MainSavePath));
        var verification = Edf6SaveCodec.Decode(modifiedEncrypted, Path.GetFileName(slot.MainSavePath));
        if (!verification.Plaintext.AsSpan().SequenceEqual(modifiedPlain))
        {
            throw new CryptographicException(UiText.T("書き込み前の暗号化・復号検証に失敗しました。", "Pre-write encryption round-trip verification failed."));
        }

        var backupDirectory = CreateBackup(
            slot,
            encrypted,
            sourceHash,
            null,
            options,
            preview.NewlyUnlocked,
            preview.WeaponsToMax,
            changedByteCount);
        var diskBytes = WriteVerifyOrRollback(
            slot.MainSavePath,
            modifiedEncrypted,
            encrypted,
            backupDirectory);
        var modifiedHash = Sha256(diskBytes);
        UpdateBackupManifest(backupDirectory, manifest => manifest with { ModifiedSha256 = modifiedHash });

        return new UnlockResult(preview, backupDirectory, sourceHash, modifiedHash, changedByteCount);
    }

    public IReadOnlyList<BackupInfo> GetBackups(SaveSlotInfo slot)
    {
        var slotBackupRoot = GetSlotBackupRoot(slot);
        if (!Directory.Exists(slotBackupRoot))
        {
            return [];
        }

        var backups = new List<BackupInfo>();
        foreach (var directory in Directory.EnumerateDirectories(slotBackupRoot))
        {
            var manifestPath = Path.Combine(directory, ManifestFileName);
            var mainSavePath = Path.Combine(directory, "MAIN.GST");
            if (!File.Exists(manifestPath) || !File.Exists(mainSavePath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<BackupManifest>(
                    File.ReadAllText(manifestPath), JsonOptions);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.SourceSlotPath) ||
                    !IsSha256(manifest.OriginalSha256) ||
                    (manifest.ModifiedSha256 is not null && !IsSha256(manifest.ModifiedSha256)) ||
                    !PathsEqual(manifest.SourceSlotPath, slot.DirectoryPath))
                {
                    continue;
                }

                backups.Add(new BackupInfo(
                    directory,
                    mainSavePath,
                    manifest.CreatedAt.LocalDateTime,
                    manifest.SourceSlotPath,
                    manifest.OriginalSha256,
                    manifest.ModifiedSha256));
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                // Ignore incomplete or foreign backup directories.
            }
        }

        return backups.OrderByDescending(backup => backup.CreatedAt).ToArray();
    }

    public RestoreResult Restore(SaveSlotInfo slot, BackupInfo backup)
    {
        _ensureGameIsClosed();
        if (!PathsEqual(backup.SourceSlotPath, slot.DirectoryPath))
        {
            throw new InvalidOperationException(UiText.T("選択したバックアップは別のセーブスロットのものです。", "The selected backup belongs to another save slot."));
        }

        var backupBytes = ReadStableFile(backup.MainSavePath);
        _ = Edf6SaveCodec.Decode(backupBytes, "MAIN.GST");
        var backupHash = Sha256(backupBytes);
        if (!string.Equals(backupHash, backup.OriginalSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SaveValidationException(UiText.T("バックアップのハッシュが記録と一致しません。", "The backup hash does not match its manifest."));
        }

        var currentBytes = ReadStableFile(slot.MainSavePath);
        _ = Edf6SaveCodec.Decode(currentBytes, "MAIN.GST");
        var currentHash = Sha256(currentBytes);
        var safetyBackup = CreateBackup(slot, currentBytes, currentHash, null, null, null, null, null);

        _ = WriteVerifyOrRollback(
            slot.MainSavePath,
            backupBytes,
            currentBytes,
            safetyBackup);
        return new RestoreResult(backup.DirectoryPath, safetyBackup, backupHash);
    }

    private static UnlockPreview InspectBytes(
        SaveSlotInfo slot,
        UnlockOptions options,
        byte[] encrypted)
    {
        var decoded = Edf6SaveCodec.Decode(encrypted, Path.GetFileName(slot.MainSavePath));
        var selected = 0;
        var newlyUnlocked = 0;
        var visibilityRepairs = 0;
        var weaponsToMax = 0;
        var alreadyMaxed = 0;
        var alreadyOwned = 0;
        var unavailableExpansion = 0;
        var inconsistent = 0;
        var entitlementSkipped = 0;
        var ownedEntitlementIncluded = 0;

        for (var weaponId = 0; weaponId < WeaponCatalog.KnownWeaponCount; weaponId++)
        {
            if (WeaponCatalog.SentinelIds.Contains(weaponId))
            {
                continue;
            }

            if ((WeaponCatalog.IsDlc1(weaponId) && !options.IncludeDlc1) ||
                (WeaponCatalog.IsDlc2(weaponId) && !options.IncludeDlc2))
            {
                unavailableExpansion++;
                continue;
            }

            var record = Edf6SaveCodec.ReadWeapon(decoded.Plaintext, weaponId);
            if (WeaponCatalog.IsEntitlementOnly(weaponId))
            {
                if (!record.IsOwned && !record.NeedsVisibilityRepair)
                {
                    entitlementSkipped++;
                    continue;
                }

                ownedEntitlementIncluded++;
            }

            selected++;
            if (record.NeedsVisibilityRepair)
            {
                visibilityRepairs++;
            }
            else if (record.IsLocked)
            {
                newlyUnlocked++;
                if (record.StatStars.Any(value => value != 0))
                {
                    inconsistent++;
                }
            }
            else
            {
                alreadyOwned++;
            }

            if (record.IsMaxed(weaponId))
            {
                alreadyMaxed++;
            }
            else
            {
                weaponsToMax++;
            }
        }

        return new UnlockPreview(
            slot.MainSavePath,
            options,
            selected,
            newlyUnlocked,
            visibilityRepairs,
            weaponsToMax,
            alreadyMaxed,
            alreadyOwned,
            WeaponCatalog.SentinelIds.Count,
            entitlementSkipped,
            ownedEntitlementIncluded,
            unavailableExpansion,
            inconsistent,
            Sha256(encrypted),
            File.GetLastWriteTime(slot.MainSavePath),
            decoded.ChecksumValid,
            decoded.StoredChecksum,
            decoded.ComputedChecksum);
    }

    private string CreateBackup(
        SaveSlotInfo slot,
        byte[] mainSaveBytes,
        string originalSha256,
        string? modifiedSha256,
        UnlockOptions? options,
        int? newlyUnlocked,
        int? weaponsMaxed,
        int? changedByteCount)
    {
        var root = GetSlotBackupRoot(slot);
        Directory.CreateDirectory(root);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss.fff");
        var baseName = $"{stamp}-{originalSha256[..12]}";
        var directory = Path.Combine(root, baseName);
        for (var suffix = 1; Directory.Exists(directory); suffix++)
        {
            directory = Path.Combine(root, $"{baseName}-{suffix}");
        }
        Directory.CreateDirectory(directory);

        var backupPath = Path.Combine(directory, "MAIN.GST");
        WriteNewFile(backupPath, mainSaveBytes);
        if (!ReadStableFile(backupPath).AsSpan().SequenceEqual(mainSaveBytes))
        {
            throw new IOException(UiText.T("バックアップの書き込み検証に失敗しました。セーブは変更していません。", "Backup verification failed. The save was not changed."));
        }
        var manifest = new BackupManifest
        {
            Tool = ToolName,
            ToolVersion = ToolVersion,
            CreatedAt = DateTimeOffset.Now,
            SourceSlotPath = Path.GetFullPath(slot.DirectoryPath),
            SourceMainSavePath = Path.GetFullPath(slot.MainSavePath),
            OriginalSha256 = originalSha256,
            ModifiedSha256 = modifiedSha256,
            Options = options,
            NewlyUnlocked = newlyUnlocked,
            WeaponsMaxed = weaponsMaxed,
            ChangedByteCount = changedByteCount,
        };
        File.WriteAllText(
            Path.Combine(directory, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));

        return directory;
    }

    private static void UpdateBackupManifest(
        string backupDirectory,
        Func<BackupManifest, BackupManifest> update)
    {
        var path = Path.Combine(backupDirectory, ManifestFileName);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new SaveValidationException(UiText.T("バックアップの記録を読み込めません。", "Backup manifest could not be read."));
        var updated = update(manifest);
        File.WriteAllText(path, JsonSerializer.Serialize(updated, JsonOptions));
    }

    private string GetSlotBackupRoot(SaveSlotInfo slot)
    {
        var safeSteamId = SanitizePathPart(slot.SteamId);
        var safeSlot = SanitizePathPart(slot.SlotName);
        return Path.Combine(_backupRoot, safeSteamId, safeSlot);
    }

    private static string SanitizePathPart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }

    private static void ValidateOptions(SaveSlotInfo slot, UnlockOptions options)
    {
        if (options.AllowDlc2WithoutMissionSave && !options.IncludeDlc2)
        {
            throw new InvalidOperationException(
                UiText.T("DLC2の手動指定にはDLC2武器の選択が必要です。", "The manual DLC2 override requires DLC2 weapons to be selected."));
        }

        if (options.IncludeDlc1 && !slot.HasDlc1)
        {
            throw new InvalidOperationException(UiText.T("このスロットではDLC1データが見つかりません。", "DLC1 was not detected in this save slot."));
        }

        if (options.IncludeDlc2 && !slot.HasDlc2 && !options.AllowDlc2WithoutMissionSave)
        {
            throw new InvalidOperationException(
                UiText.T("DLC2ミッションデータが見つかりません。DLC2を所有している場合のみ手動で指定してください。", "DLC2 mission data was not detected. Select the manual DLC2 override only if you own DLC2."));
        }
    }

    private static byte[] ReadStableFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > int.MaxValue)
        {
            throw new SaveValidationException(UiText.T("セーブファイルのサイズが不正です。", "The save file length is invalid."));
        }

        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void AtomicWrite(string path, ReadOnlySpan<byte> bytes, Action? beforeReplace = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException(UiText.T("セーブの親フォルダーがありません。", "Save path has no parent directory."));
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var originalAttributes = File.GetAttributes(path);

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.SetAttributes(tempPath, originalAttributes);
            beforeReplace?.Invoke();
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private byte[] WriteVerifyOrRollback(
        string path,
        byte[] candidate,
        byte[] rollbackBytes,
        string backupDirectory)
    {
        var replaced = false;
        try
        {
            AtomicWrite(path, candidate, () =>
            {
                _ensureGameIsClosed();
                if (!ReadStableFile(path).AsSpan().SequenceEqual(rollbackBytes))
                {
                    throw new InvalidOperationException(UiText.T(
                        "処理中にMAIN.GSTが更新されました。上書きせずに中止しました。再度プレビューしてください。",
                        "MAIN.GST changed during the operation. Stopped without overwriting it. Preview again."));
                }
            });
            replaced = true;
            var diskBytes = ReadStableFile(path);
            if (!diskBytes.AsSpan().SequenceEqual(candidate))
            {
                throw new IOException(UiText.T("書き込み後のデータが予定したセーブと一致しません。", "The bytes read after writing did not match the candidate save."));
            }

            _ = Edf6SaveCodec.Decode(diskBytes, Path.GetFileName(path));
            return diskBytes;
        }
        catch (Exception writeError) when (replaced)
        {
            try
            {
                AtomicWrite(path, rollbackBytes, () =>
                {
                    _ensureGameIsClosed();
                    if (!ReadStableFile(path).AsSpan().SequenceEqual(candidate))
                    {
                        throw new IOException(UiText.T("書き込み後に別の変更を検出しました。自動復元で上書きしません。", "Detected another change after writing. Automatic rollback will not overwrite it."));
                    }
                });
                var rolledBack = ReadStableFile(path);
                if (!rolledBack.AsSpan().SequenceEqual(rollbackBytes))
                {
                    throw new IOException(UiText.T("自動復元したデータが元のセーブと一致しません。", "Rollback bytes did not match the original save."));
                }

                _ = Edf6SaveCodec.Decode(rolledBack, Path.GetFileName(path));
                throw new IOException(
                    UiText.T($"書き込み検証に失敗したため元のセーブを自動復元しました。バックアップ: {backupDirectory}", $"Save write verification failed, so the original was restored automatically. " +
                    $"Backup: {backupDirectory}"),
                    writeError);
            }
            catch (IOException rollbackReport) when (rollbackReport.InnerException == writeError)
            {
                throw;
            }
            catch (Exception rollbackError)
            {
                throw new IOException(
                    UiText.T($"書き込み検証と自動復元に失敗しました。確認済みのバックアップを使用してください: {backupDirectory}", $"Save write verification and automatic rollback both failed. " +
                    $"Use the verified backup in: {backupDirectory}"),
                    new AggregateException(writeError, rollbackError));
            }
        }
    }

    private static void WriteNewFile(string path, ReadOnlySpan<byte> bytes)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void ValidateOnlyExpectedBytesChanged(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after,
        HashSet<int> changedWeaponIds)
    {
        if (before.Length != after.Length)
        {
            throw new SaveValidationException(UiText.T("変更後のセーブサイズが予期せず変化しました。", "The modified save length changed unexpectedly."));
        }

        for (var offset = 0; offset < before.Length; offset++)
        {
            if (before[offset] == after[offset])
            {
                continue;
            }

            if (offset is >= Edf6SaveCodec.ChecksumFieldOffset and
                < Edf6SaveCodec.ChecksumFieldOffset + sizeof(uint))
            {
                continue;
            }

            if (offset < Edf6SaveCodec.WeaponTableOffset)
            {
                throw new SaveValidationException(UiText.T($"武器テーブル外の0x{offset:X}に予期しない変更を検出しました。", $"Unexpected change outside the weapon table at 0x{offset:X}."));
            }

            var relative = offset - Edf6SaveCodec.WeaponTableOffset;
            var weaponId = relative / Edf6SaveCodec.WeaponRecordSize;
            if (!changedWeaponIds.Contains(weaponId))
            {
                throw new SaveValidationException(
                    UiText.T($"武器レコード{weaponId}の0x{offset:X}に予期しない変更を検出しました。", $"Unexpected change to weapon record {weaponId} at 0x{offset:X}."));
            }
        }
    }

    private static int CountWeaponTableChanges(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var changed = 0;
        for (var offset = Edf6SaveCodec.WeaponTableOffset; offset < Edf6SaveCodec.ExpectedMainSaveLength; offset++)
        {
            if (before[offset] != after[offset])
            {
                changed++;
            }
        }

        return changed;
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}

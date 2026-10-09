using System.Text.Json.Serialization;

namespace EDF6WeaponUnlocker.Core;

public sealed record SaveSlotInfo(
    string SteamId,
    string SlotName,
    string DirectoryPath,
    string MainSavePath,
    DateTime LastWriteTime,
    bool HasDlc1,
    bool HasDlc2)
{
    public override string ToString()
    {
        var dlc = (HasDlc1, HasDlc2) switch
        {
            (true, true) => UiText.T(" / DLC1・DLC2検出", " / DLC1 & DLC2 detected"),
            (true, false) => UiText.T(" / DLC1検出", " / DLC1 detected"),
            (false, true) => UiText.T(" / DLC2検出", " / DLC2 detected"),
            _ => string.Empty,
        };

        return $"{SlotName} — {LastWriteTime:yyyy/MM/dd HH:mm}{dlc}";
    }
}

public sealed record UnlockOptions(
    bool IncludeDlc1,
    bool IncludeDlc2,
    bool AllowDlc2WithoutMissionSave = false);

public sealed record WeaponRecord(uint OwnershipFlags, byte[] StatStars)
{
    // Bit 0 is the actual ownership bit. Other observed bits are transient
    // weapon-state flags (for example 3 and 9 are both owned records).
    public bool IsOwned => (OwnershipFlags & 1u) != 0;

    // v1.1.0/v1.1.1 accidentally replaced the flag word with 8 while applying
    // ★MAX. That clears bit 0 and makes the weapon disappear. This exact shape
    // lets the corrected release repair affected saves, including owned
    // entitlement records, without unlocking untouched paid items.
    public bool NeedsVisibilityRepair =>
        OwnershipFlags == 8u && StatStars.All(value => value >= 8);

    public bool NeedsLegacyMaxNormalization =>
        OwnershipFlags is 8u or 9u && StatStars.All(value => value >= 8);

    public bool IsLocked => !IsOwned;

    public bool IsMaxed(int weaponId) =>
        IsOwned && WeaponStarCatalog.IsAtMax(weaponId, StatStars);
}

public sealed record UnlockPreview(
    string SavePath,
    UnlockOptions Options,
    int SelectedWeapons,
    int NewlyUnlocked,
    int VisibilityRepairs,
    int WeaponsToMax,
    int AlreadyMaxed,
    int AlreadyOwned,
    int ProtectedSlotsSkipped,
    int EntitlementItemsSkipped,
    int OwnedEntitlementItemsIncluded,
    int UnavailableExpansionWeaponsSkipped,
    int InconsistentRecords,
    string SourceSha256,
    DateTime SourceLastWriteTime,
    bool ChecksumValid,
    uint StoredChecksum,
    uint ComputedChecksum);

public sealed record UnlockResult(
    UnlockPreview Preview,
    string BackupDirectory,
    string OriginalSha256,
    string ModifiedSha256,
    int ChangedByteCount);

public sealed record RestoreResult(
    string RestoredFromDirectory,
    string SafetyBackupDirectory,
    string RestoredSha256);

public sealed record BackupInfo(
    string DirectoryPath,
    string MainSavePath,
    DateTime CreatedAt,
    string SourceSlotPath,
    string OriginalSha256,
    string? ModifiedSha256)
{
    public override string ToString() => $"{CreatedAt:yyyy/MM/dd HH:mm:ss} — {OriginalSha256[..Math.Min(12, OriginalSha256.Length)]}…";
}

public sealed record BackupManifest
{
    public required string Tool { get; init; }
    public required string ToolVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string SourceSlotPath { get; init; }
    public required string SourceMainSavePath { get; init; }
    public required string OriginalSha256 { get; init; }
    public string? ModifiedSha256 { get; init; }
    public UnlockOptions? Options { get; init; }
    public int? NewlyUnlocked { get; init; }
    public int? WeaponsMaxed { get; init; }
    public int? ChangedByteCount { get; init; }

    [JsonIgnore]
    public string? ManifestPath { get; init; }
}

public sealed class SaveValidationException : Exception
{
    public SaveValidationException(string message) : base(message)
    {
    }
}

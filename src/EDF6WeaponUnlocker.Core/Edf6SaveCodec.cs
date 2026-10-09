using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EDF6WeaponUnlocker.Core;

public sealed record DecodedEdf6Save(
    byte[] Plaintext,
    uint StoredChecksum,
    uint ComputedChecksum,
    bool ChecksumValid);

public static class Edf6SaveCodec
{
    public const int ChecksumFieldOffset = 0x0C;
    public const int ChecksumRegionOffset = 0x14;
    public const int WeaponTableOffset = 0x7CFC;
    public const int WeaponRecordSize = 12;
    public const int WeaponTableLength = 0x6000;
    public const int ExpectedMainSaveLength = WeaponTableOffset + WeaponTableLength;

    private static readonly byte[] KeySuffix = Encoding.ASCII.GetBytes("Edf5.*_Steam_Ver");

    public static DecodedEdf6Save Decode(
        ReadOnlySpan<byte> ciphertext,
        string fileName,
        bool requireValidChecksum = true)
    {
        EnsureMainSaveName(fileName);
        var (key, iv) = GenerateKeyAndIv(fileName);
        try
        {
            var plaintext = AesCtr.Transform(ciphertext, key, iv);
            ValidateStructure(plaintext);

            var stored = BinaryPrimitives.ReadUInt32LittleEndian(
                plaintext.AsSpan(ChecksumFieldOffset, sizeof(uint)));
            var computed = Crc32C.ComputeEdf6StoredValue(plaintext.AsSpan(ChecksumRegionOffset));
            var valid = stored == computed;
            if (requireValidChecksum && !valid)
            {
                throw new SaveValidationException(
                    UiText.T($"MAIN.GSTのチェックサムが不一致です（記録値0x{stored:X8}、計算値0x{computed:X8}）。", $"MAIN.GST checksum mismatch (stored 0x{stored:X8}, computed 0x{computed:X8})."));
            }

            return new DecodedEdf6Save(plaintext, stored, computed, valid);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(iv);
        }
    }

    public static byte[] Encode(ReadOnlySpan<byte> plaintext, string fileName)
    {
        EnsureMainSaveName(fileName);
        ValidateStructure(plaintext);

        var normalized = plaintext.ToArray();
        UpdateChecksum(normalized);

        var (key, iv) = GenerateKeyAndIv(fileName);
        try
        {
            return AesCtr.Transform(normalized, key, iv);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(iv);
            CryptographicOperations.ZeroMemory(normalized);
        }
    }

    public static uint UpdateChecksum(Span<byte> plaintext)
    {
        ValidateStructure(plaintext);
        var checksum = Crc32C.ComputeEdf6StoredValue(plaintext[ChecksumRegionOffset..]);
        BinaryPrimitives.WriteUInt32LittleEndian(
            plaintext.Slice(ChecksumFieldOffset, sizeof(uint)), checksum);
        return checksum;
    }

    public static WeaponRecord ReadWeapon(ReadOnlySpan<byte> plaintext, int weaponId)
    {
        ValidateWeaponId(weaponId);
        if (plaintext.Length < ExpectedMainSaveLength)
        {
            throw new SaveValidationException(UiText.T("MAIN.GSTが短すぎるため武器テーブルを読み込めません。", "MAIN.GST is too short for the EDF6 weapon table."));
        }

        var offset = WeaponTableOffset + (weaponId * WeaponRecordSize);
        var ownershipFlags = BinaryPrimitives.ReadUInt32LittleEndian(
            plaintext.Slice(offset, sizeof(uint)));
        var stats = plaintext.Slice(offset + sizeof(uint), 8).ToArray();
        return new WeaponRecord(ownershipFlags, stats);
    }

    public static void SetWeaponToMax(Span<byte> plaintext, int weaponId)
    {
        ValidateWeaponId(weaponId);
        if (plaintext.Length < ExpectedMainSaveLength)
        {
            throw new SaveValidationException(UiText.T("MAIN.GSTが短すぎるため武器テーブルを読み込めません。", "MAIN.GST is too short for the EDF6 weapon table."));
        }

        var offset = WeaponTableOffset + (weaponId * WeaponRecordSize);
        var ownershipSpan = plaintext.Slice(offset, sizeof(uint));
        var ownershipFlags = BinaryPrimitives.ReadUInt32LittleEndian(ownershipSpan);
        var stats = plaintext.Slice(offset + sizeof(uint), 8);
        var normalizeLegacyBug = ownershipFlags is 8u or 9u &&
            stats.IndexOfAnyExceptInRange((byte)8, byte.MaxValue) < 0;

        if ((ownershipFlags & 1u) == 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(ownershipSpan, ownershipFlags | 1u);
        }

        Span<byte> maximums = stackalloc byte[8];
        WeaponStarCatalog.CopyMaxStars(weaponId, maximums);
        for (var index = 0; index < stats.Length; index++)
        {
            var maximum = maximums[index];
            if (maximum == 0)
            {
                stats[index] = 0;
            }
            else if (normalizeLegacyBug || stats[index] < maximum)
            {
                stats[index] = maximum;
            }
        }
    }

    public static (byte[] Key, byte[] Iv) GenerateKeyAndIv(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var keyPrefix = MD5.HashData(Encoding.Unicode.GetBytes($"edf6{name}.sav"));
        var iv = MD5.HashData(Encoding.Unicode.GetBytes($"edf6{name}.stm"));
        var key = new byte[32];
        keyPrefix.CopyTo(key, 0);
        KeySuffix.CopyTo(key, 16);
        CryptographicOperations.ZeroMemory(keyPrefix);
        return (key, iv);
    }

    public static void ValidateStructure(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length != ExpectedMainSaveLength)
        {
            throw new SaveValidationException(
                UiText.T($"未対応のMAIN.GSTサイズ: {plaintext.Length}バイト（想定{ExpectedMainSaveLength}）。ゲームの更新等が考えられます。", $"Unsupported MAIN.GST size: {plaintext.Length} bytes (expected {ExpectedMainSaveLength}). " +
                "The game may have changed; no data was written."));
        }

        if (!plaintext[..4].SequenceEqual("MDB0"u8))
        {
            throw new SaveValidationException(
                UiText.T("MAIN.GSTをMDB0形式に復号できません。ファイル・プラットフォーム・ゲームのバージョンが未対応です。", "MAIN.GST did not decrypt to an MDB0 header. The file, platform, or game version is unsupported."));
        }
    }

    private static void EnsureMainSaveName(string fileName)
    {
        if (!string.Equals(Path.GetFileName(fileName), "MAIN.GST", StringComparison.OrdinalIgnoreCase))
        {
            throw new SaveValidationException(UiText.T("Steam版EDF6のMAIN.GSTのみ対応しています。", "Only the Steam EDF6 MAIN.GST file is supported."));
        }
    }

    private static void ValidateWeaponId(int weaponId)
    {
        if (weaponId is < 0 or >= WeaponCatalog.RecordCount)
        {
            throw new ArgumentOutOfRangeException(nameof(weaponId));
        }
    }
}

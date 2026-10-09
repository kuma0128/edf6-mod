using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EDF6WeaponUnlocker.Core;

var tests = new (string Name, Action Run)[]
{
    ("CRC-32C standard vector", TestCrc32C),
    ("AES-256 CTR NIST vector", TestAesCtr),
    ("EDF6 codec round-trip and tamper detection", TestCodec),
    ("Weapon catalog boundaries", TestCatalog),
    ("Per-weapon maximum-star catalog", TestStarCatalog),
    ("★MAX preserves ownership flags and repairs v1.1 visibility", TestMaxWeapon),
    ("Transactional ★MAX and restore", TestTransactionalIntegration),
    ("Manual DLC2 override unlocks exactly the DLC2 weapon range", TestManualDlc2Override),
    ("Commit guards preserve externally updated saves and stop when game starts", TestCommitGuards),
    ("Malformed backup manifests are skipped", TestMalformedBackups),
    ("Japanese / English preference and errors", TestLanguages),
};

if (args.Length == 1) tests = [..tests, ("Packaged CLI bilingual and transactional integration", () => TestPackagedCli(args[0]))];
var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL  {test.Name}");
        Console.Error.WriteLine($"      {exception}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static void TestCrc32C()
{
    var data = Encoding.ASCII.GetBytes("123456789");
    Equal(0xE3069283u, Crc32C.Compute(data));
    Equal(0x1CF96D7Cu, Crc32C.ComputeEdf6StoredValue(data));
}

static void TestAesCtr()
{
    var key = Hex(
        "603deb1015ca71be2b73aef0857d7781" +
        "1f352c073b6108d72d9810a30914dff4");
    var iv = Hex("f0f1f2f3f4f5f6f7f8f9fafbfcfdfeff");
    var plaintext = Hex(
        "6bc1bee22e409f96e93d7e117393172a" +
        "ae2d8a571e03ac9c9eb76fac45af8e51" +
        "30c81c46a35ce411e5fbc1191a0a52ef" +
        "f69f2445df4f9b17ad2b417be66c3710");
    var expected = Hex(
        "601ec313775789a5b7a7f504bbf3d228" +
        "f443e3ca4d62b59aca84e990cacaf5c5" +
        "2b0930daa23de94ce87017ba2d84988d" +
        "dfc9c58db67aada613c2dd08457941a6");

    var encrypted = AesCtr.Transform(plaintext, key, iv);
    SequenceEqual(expected, encrypted);
    SequenceEqual(plaintext, AesCtr.Transform(encrypted, key, iv));
}

static void TestCodec()
{
    var plaintext = CreatePlainSave();
    var encrypted = Edf6SaveCodec.Encode(plaintext, "MAIN.GST");
    var decoded = Edf6SaveCodec.Decode(encrypted, "MAIN.GST");
    True(decoded.ChecksumValid, "Checksum should validate after round-trip.");
    SequenceEqual(plaintext, decoded.Plaintext);

    encrypted[^1] ^= 0x01;
    Throws<SaveValidationException>(() => Edf6SaveCodec.Decode(encrypted, "MAIN.GST"));
}

static void TestCatalog()
{
    var baseOnly = new UnlockOptions(false, false);
    var withDlc1 = new UnlockOptions(true, false);
    var withBoth = new UnlockOptions(true, true);

    Equal(1354, CountSelected(baseOnly));
    Equal(1430, CountSelected(withDlc1));
    Equal(1540, CountSelected(withBoth));
    True(!WeaponCatalog.IsSelected(305, withBoth), "None sentinel must not be selected.");
    True(!WeaponCatalog.IsSelected(1346, withBoth), "Locked entitlement must not be selected.");
    True(WeaponCatalog.IsSelected(1346, withBoth, entitlementItemIsOwned: true),
        "Owned entitlement should be selected for ★MAX.");
}

static void TestMaxWeapon()
{
    var plaintext = CreatePlainSave();
    const int id = 454;
    var offset = Edf6SaveCodec.WeaponTableOffset + (id * Edf6SaveCodec.WeaponRecordSize);
    BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(offset, 4), 0);
    new byte[] { 0, 1, 7, 8, 9, 255, 2, 0 }.CopyTo(plaintext, offset + 4);

    Edf6SaveCodec.SetWeaponToMax(plaintext, id);
    var record = Edf6SaveCodec.ReadWeapon(plaintext, id);
    Equal(1u, record.OwnershipFlags);
    SequenceEqual(new byte[] { 8, 4, 8, 8, 9, 255, 0, 0 }, record.StatStars);
    True(record.IsOwned, "A newly unlocked record must have ownership bit 0 set.");
    True(record.IsMaxed(id), "The record should be considered maxed.");

    const int ownedId = 43;
    SetRecord(plaintext, ownedId, 3, Enumerable.Repeat((byte)1, 8).ToArray());
    Edf6SaveCodec.SetWeaponToMax(plaintext, ownedId);
    var ownedRecord = Edf6SaveCodec.ReadWeapon(plaintext, ownedId);
    Equal(3u, ownedRecord.OwnershipFlags);
    True(ownedRecord.IsMaxed(ownedId), "Existing non-ownership flag bits must be preserved.");

    const int legacyBrokenId = 44;
    SetRecord(plaintext, legacyBrokenId, 8, Enumerable.Repeat((byte)8, 8).ToArray());
    var legacyBrokenRecord = Edf6SaveCodec.ReadWeapon(plaintext, legacyBrokenId);
    True(legacyBrokenRecord.NeedsVisibilityRepair,
        "The exact v1.1 hidden-weapon record must be recognized.");
    True(!legacyBrokenRecord.IsMaxed(legacyBrokenId),
        "A record without the ownership bit must not be reported as maxed.");
    Edf6SaveCodec.SetWeaponToMax(plaintext, legacyBrokenId);
    var repairedRecord = Edf6SaveCodec.ReadWeapon(plaintext, legacyBrokenId);
    Equal(9u, repairedRecord.OwnershipFlags);
    True(repairedRecord.IsMaxed(legacyBrokenId),
        "The v1.1 hidden-weapon record must be repaired to owned.");
}

static void TestStarCatalog()
{
    SequenceEqual(new byte[] { 7, 8, 6, 8, 8, 4, 0, 0 },
        WeaponStarCatalog.GetMaxStars(0));
    SequenceEqual(new byte[] { 8, 4, 8, 5, 5, 8, 0, 0 },
        WeaponStarCatalog.GetMaxStars(454));
    SequenceEqual(new byte[] { 7, 10, 8, 0, 0, 0, 0, 0 },
        WeaponStarCatalog.GetMaxStars(604));
}

static void TestTransactionalIntegration()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"edf6wu-selftest-{Guid.NewGuid():N}");
    var slotDirectory = Path.Combine(testRoot, "76561198000000000", "saveslot00");
    var backupRoot = Path.Combine(testRoot, "Backups");
    Directory.CreateDirectory(slotDirectory);

    try
    {
        var plaintext = CreatePlainSave();
        SetRecord(plaintext, 1346, 1, Enumerable.Repeat((byte)1, 8).ToArray());
        Edf6SaveCodec.UpdateChecksum(plaintext);
        var originalEncrypted = Edf6SaveCodec.Encode(plaintext, "MAIN.GST");
        var mainPath = Path.Combine(slotDirectory, "MAIN.GST");
        File.WriteAllBytes(mainPath, originalEncrypted);
        File.WriteAllBytes(Path.Combine(slotDirectory, "DEFP_DLC1.MST"), [0]);

        var slot = new SaveSlotInfo(
            "76561198000000000",
            "saveslot00",
            slotDirectory,
            mainPath,
            File.GetLastWriteTime(mainPath),
            HasDlc1: true,
            HasDlc2: false);
        var options = new UnlockOptions(true, false);
        var service = new UnlockService(backupRoot, ensureGameIsClosed: () => { });

        var before = service.Inspect(slot, options);
        Equal(1431, before.SelectedWeapons);
        Equal(1430, before.NewlyUnlocked);
        Equal(1431, before.WeaponsToMax);
        Equal(1, before.OwnedEntitlementItemsIncluded);
        Equal(19, before.EntitlementItemsSkipped);

        var applied = service.Apply(slot, options, before.SourceSha256);
        True(applied.ChangedByteCount > 0, "Apply should change weapon table bytes.");
        True(Directory.Exists(applied.BackupDirectory), "Backup directory should exist.");

        var after = service.Inspect(slot, options);
        Equal(0, after.NewlyUnlocked);
        Equal(0, after.WeaponsToMax);
        Equal(1431, after.AlreadyMaxed);

        var decodedAfter = Edf6SaveCodec.Decode(File.ReadAllBytes(mainPath), "MAIN.GST");
        True(Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, 1346).IsMaxed(1346),
            "Owned entitlement should be maxed.");
        True(Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, 1347).IsLocked,
            "Unowned entitlement should remain locked.");
        True(Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, 305).IsLocked,
            "None sentinel should remain untouched.");
        True(Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, 1451).IsLocked,
            "Unavailable DLC2 should remain locked.");

        var backup = service.GetBackups(slot).Single();
        _ = service.Restore(slot, backup);
        SequenceEqual(originalEncrypted, File.ReadAllBytes(mainPath));
    }
    finally
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}

static void TestManualDlc2Override()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"edf6wu-dlc2-selftest-{Guid.NewGuid():N}");
    var slotDirectory = Path.Combine(testRoot, "76561198000000000", "saveslot00");
    var backupRoot = Path.Combine(testRoot, "Backups");
    Directory.CreateDirectory(slotDirectory);

    try
    {
        var plaintext = CreatePlainSave();
        var originalEncrypted = Edf6SaveCodec.Encode(plaintext, "MAIN.GST");
        var mainPath = Path.Combine(slotDirectory, "MAIN.GST");
        File.WriteAllBytes(mainPath, originalEncrypted);
        File.WriteAllBytes(Path.Combine(slotDirectory, "DEFP_DLC1.MST"), [0]);

        var slot = new SaveSlotInfo(
            "76561198000000000",
            "saveslot00",
            slotDirectory,
            mainPath,
            File.GetLastWriteTime(mainPath),
            HasDlc1: true,
            HasDlc2: false);
        var service = new UnlockService(backupRoot, ensureGameIsClosed: () => { });

        Throws<InvalidOperationException>(() =>
            service.Inspect(slot, new UnlockOptions(true, true)));
        Throws<InvalidOperationException>(() =>
            service.Inspect(slot, new UnlockOptions(true, false, true)));

        var options = new UnlockOptions(
            IncludeDlc1: true,
            IncludeDlc2: true,
            AllowDlc2WithoutMissionSave: true);
        var before = service.Inspect(slot, options);
        Equal(1540, before.SelectedWeapons);
        Equal(1540, before.NewlyUnlocked);
        Equal(1540, before.WeaponsToMax);
        Equal(0, before.UnavailableExpansionWeaponsSkipped);

        _ = service.Apply(slot, options, before.SourceSha256);

        var after = service.Inspect(slot, options);
        Equal(0, after.NewlyUnlocked);
        Equal(0, after.WeaponsToMax);
        Equal(1540, after.AlreadyMaxed);

        var decodedAfter = Edf6SaveCodec.Decode(File.ReadAllBytes(mainPath), "MAIN.GST");
        var maxedDlc2 = Enumerable.Range(
                WeaponCatalog.Dlc2Start,
                WeaponCatalog.Dlc2End - WeaponCatalog.Dlc2Start + 1)
            .Count(id => Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, id).IsMaxed(id));
        Equal(110, maxedDlc2);
        True(Edf6SaveCodec.ReadWeapon(decodedAfter.Plaintext, 1561).IsLocked,
            "The entitlement record immediately after DLC2 must remain locked.");
        True(!File.Exists(Path.Combine(slotDirectory, "DEFP_DLC2.MST")),
            "The manual DLC2 override must not create or alter DLC2 mission data.");
    }
    finally
    {
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}

static void TestPackagedCli(string executable)
{
    var root = Path.Combine(Path.GetTempPath(), $"edf6wu-cli-{Guid.NewGuid():N}");
    var dir = Path.Combine(root, "account", "saveslot00"); Directory.CreateDirectory(dir);
    try
    {
        var path = Path.Combine(dir, "MAIN.GST"); var backup = Path.Combine(root, "Backups");
        var original = Edf6SaveCodec.Encode(CreatePlainSave(), "MAIN.GST"); File.WriteAllBytes(path, original);
        (int Code, string Output, string Error) Run(params string[] command)
        {
            var start = new System.Diagnostics.ProcessStartInfo(Path.GetFullPath(executable))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var argument in command) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            process.StandardInput.WriteLine("CANCEL"); process.StandardInput.Close();
            if (!process.WaitForExit(20000)) { process.Kill(); throw new TimeoutException("Packaged CLI timeout"); }
            return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }
        foreach (var lang in new[] { "ja", "en" })
        {
            var help = Run("--lang", lang, "help"); Equal(0, help.Code);
            True(help.Output.Contains(lang == "ja" ? "所有者のみ" : "only if you own"), "CLI help language");
        }
        foreach (var command in new[] {
            new[] { "unlock", "--slot" }, new[] { "unlock", "--slot", "--yes" },
            new[] { "unlock", "--slot", dir, "--slot", dir },
            new[] { "unlock", "--slot", dir, "--unknown", "--yes" },
            new[] { "unlock", "--slot", dir, "--include-dlc2", "--no-dlc2", "--yes" },
            new[] { "unlock", "--lang", "invalid", "--slot", dir, "--yes" },
        }) { True(Run(command).Code != 0, "Invalid CLI arguments must be refused"); SequenceEqual(original, File.ReadAllBytes(path)); }
        var preview = Run("inspect", "--lang", "en", "--slot", dir, "--json"); Equal(0, preview.Code);
        using (var json = System.Text.Json.JsonDocument.Parse(preview.Output)) True(json.RootElement.GetProperty("WeaponsToMax").GetInt32() > 0, "JSON preview");
        Equal(3, Run("unlock", "--slot", dir).Code); SequenceEqual(original, File.ReadAllBytes(path));
        var applied = Run("unlock", "--lang", "en", "--slot", dir, "--backup-root", backup, "--yes", "--json"); Equal(0, applied.Code);
        using (var json = System.Text.Json.JsonDocument.Parse(applied.Output)) True(Directory.Exists(json.RootElement.GetProperty("BackupDirectory").GetString()), "CLI backup");
        var after = Run("inspect", "--lang", "ja", "--slot", dir, "--json"); Equal(0, after.Code);
        using (var json = System.Text.Json.JsonDocument.Parse(after.Output)) Equal(0, json.RootElement.GetProperty("WeaponsToMax").GetInt32());
        Equal(0, Run("restore-latest", "--lang", "ja", "--slot", dir, "--backup-root", backup, "--yes", "--json").Code);
        SequenceEqual(original, File.ReadAllBytes(path));
    }
    finally { DeleteTestDirectory(root, "edf6wu-cli-"); }
}

static void TestCommitGuards()
{
    var root = Path.Combine(Path.GetTempPath(), $"edf6wu-guards-{Guid.NewGuid():N}");
    var dir = Path.Combine(root, "account", "saveslot00"); Directory.CreateDirectory(dir);
    try
    {
        var path = Path.Combine(dir, "MAIN.GST");
        var original = Edf6SaveCodec.Encode(CreatePlainSave(), "MAIN.GST");
        var changedPlain = CreatePlainSave(); changedPlain[100] = 7;
        var changed = Edf6SaveCodec.Encode(changedPlain, "MAIN.GST");
        var slot = new SaveSlotInfo("account", "saveslot00", dir, path, DateTime.Now, false, false);
        var options = new UnlockOptions(false, false);
        File.WriteAllBytes(path, original);
        var calls = 0;
        var service = new UnlockService(Path.Combine(root, "Backups"), () => { if (++calls == 2) File.WriteAllBytes(path, changed); });
        Throws<InvalidOperationException>(() => service.Apply(slot, options));
        Equal(2, calls); SequenceEqual(changed, File.ReadAllBytes(path));
        // Restore is guarded too: the update occurs after its safety backup.
        var backup = service.GetBackups(slot).First();
        calls = 0;
        var restore = new UnlockService(Path.Combine(root, "Backups"), () => { if (++calls == 2) File.WriteAllBytes(path, original); });
        Throws<InvalidOperationException>(() => restore.Restore(slot, backup));
        SequenceEqual(original, File.ReadAllBytes(path));
        calls = 0;
        var gameStarted = new UnlockService(Path.Combine(root, "Backups"), () => { if (++calls == 2) throw new InvalidOperationException("Game started"); });
        Throws<InvalidOperationException>(() => gameStarted.Apply(slot, options));
        Equal(2, calls); SequenceEqual(original, File.ReadAllBytes(path));
        True(!Directory.EnumerateFiles(dir, "*.tmp").Any(), "Guard failures must clean staging files.");
    }
    finally { DeleteTestDirectory(root, "edf6wu-guards-"); }
}
static void TestMalformedBackups()
{
    var root = Path.Combine(Path.GetTempPath(), $"edf6wu-manifests-{Guid.NewGuid():N}");
    var dir = Path.Combine(root, "account", "saveslot00"); Directory.CreateDirectory(dir);
    try
    {
        var path = Path.Combine(dir, "MAIN.GST");
        File.WriteAllBytes(path, Edf6SaveCodec.Encode(CreatePlainSave(), "MAIN.GST"));
        var slot = new SaveSlotInfo("account", "saveslot00", dir, path, DateTime.Now, false, false);
        var service = new UnlockService(Path.Combine(root, "Backups"), () => { });
        var result = service.Apply(slot, new UnlockOptions(false, false));
        var manifestPath = Path.Combine(result.BackupDirectory, "manifest.json");
        var valid = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        foreach (var (key, value) in new (string, string?)[] { ("originalSha256", null), ("originalSha256", "x"), ("sourceSlotPath", null), ("modifiedSha256", "bad") })
        {
            var broken = valid.DeepClone(); broken[key] = value;
            File.WriteAllText(manifestPath, broken.ToJsonString());
            Equal(0, service.GetBackups(slot).Count);
        }
        File.WriteAllText(manifestPath, "{");
        Equal(0, service.GetBackups(slot).Count);
        File.WriteAllText(manifestPath, valid.ToJsonString());
        Equal(1, service.GetBackups(slot).Count);
        var backup = service.GetBackups(slot)[0];
        var current = File.ReadAllBytes(path);
        File.WriteAllBytes(backup.MainSavePath, [0, 1, 2]);
        Throws<SaveValidationException>(() => service.Restore(slot, backup));
        SequenceEqual(current, File.ReadAllBytes(path));
    }
    finally { DeleteTestDirectory(root, "edf6wu-manifests-"); }
}
static void TestLanguages()
{
    var root = Path.Combine(Path.GetTempPath(), $"edf6wu-language-{Guid.NewGuid():N}");
    try
    {
        var preference = new LanguagePreference("test", root);
        foreach (var lang in new[] { "ja", "en" })
        {
            UiText.SetLanguage(lang); preference.Save(lang);
            Equal(lang, preference.Load()); Equal(lang, UiText.LanguageCode);
            Equal(lang == "ja" ? "日本語" : "English", UiText.T("日本語", "English"));
            try { Edf6SaveCodec.Decode([0], "OTHER.GST"); }
            catch (SaveValidationException e) { True(e.Message.Contains(lang == "ja" ? "のみ対応" : "Only the Steam"), "Error language mismatch"); }
        }
        Throws<ArgumentException>(() => preference.Save("invalid"));
        File.WriteAllText(Path.Combine(root, "language.json"), "null");
        True(preference.Load() is "ja" or "en", "Corrupt preference must fall back.");
        File.WriteAllText(Path.Combine(root, "language.json"), "{");
        True(preference.Load() is "ja" or "en", "Truncated preference must fall back.");
    }
    finally { DeleteTestDirectory(root, "edf6wu-language-"); }
}
static void DeleteTestDirectory(string path, string prefix)
{
    var full = Path.GetFullPath(path);
    var expected = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + prefix;
    if (!full.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup");
    if (Directory.Exists(full)) Directory.Delete(full, true);
}

static byte[] CreatePlainSave()
{
    var plaintext = new byte[Edf6SaveCodec.ExpectedMainSaveLength];
    "MDB0"u8.CopyTo(plaintext);
    Edf6SaveCodec.UpdateChecksum(plaintext);
    return plaintext;
}

static void SetRecord(byte[] plaintext, int id, uint average, byte[] stats)
{
    var offset = Edf6SaveCodec.WeaponTableOffset + (id * Edf6SaveCodec.WeaponRecordSize);
    BinaryPrimitives.WriteUInt32LittleEndian(plaintext.AsSpan(offset, 4), average);
    stats.CopyTo(plaintext, offset + 4);
}

static int CountSelected(UnlockOptions options) => Enumerable
    .Range(0, WeaponCatalog.KnownWeaponCount)
    .Count(id => WeaponCatalog.IsSelected(id, options));

static byte[] Hex(string value) => Convert.FromHexString(value);

static void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void True(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void SequenceEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"Byte sequences differ. Expected {Convert.ToHexString(expected)}, got {Convert.ToHexString(actual)}.");
    }
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}

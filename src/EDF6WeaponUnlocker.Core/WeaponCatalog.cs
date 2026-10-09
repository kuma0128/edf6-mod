namespace EDF6WeaponUnlocker.Core;

public static class WeaponCatalog
{
    public const int RecordCount = 2048;
    public const int KnownWeaponCount = 1564;

    public const int Dlc1Start = 1375;
    public const int Dlc1End = 1450;
    public const int Dlc2Start = 1451;
    public const int Dlc2End = 1560;

    // Separate paid/pre-order/collaboration items. A save editor cannot establish
    // the Steam entitlement reliably, so these records are deliberately untouched.
    public static readonly (int Start, int End)[] EntitlementOnlyRanges =
    [
        (1346, 1362),
        (1561, 1563),
    ];

    // Historically protected starting weapons and "None" sentinels. Starting
    // weapons are valid ★MAX targets; only the four sentinels remain untouched.
    public static readonly HashSet<int> ProtectedIds =
    [
        0, 46, 77, 109, 165, 225, 305,
        384, 385, 462, 534, 584, 597, 619, 654,
        690, 715, 747, 771, 866, 867,
        1041, 1042, 1080, 1095, 1124, 1163, 1174, 1206, 1262,
    ];

    public static readonly HashSet<int> SentinelIds = [305, 866, 867, 1262];

    public static bool IsEntitlementOnly(int id) =>
        EntitlementOnlyRanges.Any(range => id >= range.Start && id <= range.End);

    public static bool IsDlc1(int id) => id is >= Dlc1Start and <= Dlc1End;

    public static bool IsDlc2(int id) => id is >= Dlc2Start and <= Dlc2End;

    public static bool IsKnown(int id) => id is >= 0 and < KnownWeaponCount;

    public static bool IsSelected(
        int id,
        UnlockOptions options,
        bool entitlementItemIsOwned = false)
    {
        if (!IsKnown(id) || SentinelIds.Contains(id))
        {
            return false;
        }

        if (IsEntitlementOnly(id))
        {
            return entitlementItemIsOwned;
        }

        if (IsDlc1(id))
        {
            return options.IncludeDlc1;
        }

        if (IsDlc2(id))
        {
            return options.IncludeDlc2;
        }

        return true;
    }
}

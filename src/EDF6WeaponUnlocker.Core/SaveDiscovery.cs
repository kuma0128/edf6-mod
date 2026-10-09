using System.Diagnostics;

namespace EDF6WeaponUnlocker.Core;

public static class SaveDiscovery
{
    private static readonly string[] GameProcessNames = ["EDF6", "LaunchGame"];

    public static string DefaultSaveRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EarthDefenceForce6",
        "SAVE_DATA");

    public static IReadOnlyList<SaveSlotInfo> Discover(string? saveRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(saveRoot) ? DefaultSaveRoot : saveRoot;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var results = new List<SaveSlotInfo>();
        foreach (var accountDirectory in Directory.EnumerateDirectories(root))
        {
            var steamId = Path.GetFileName(accountDirectory);
            foreach (var slotDirectory in Directory.EnumerateDirectories(accountDirectory, "saveslot*"))
            {
                var mainSavePath = Path.Combine(slotDirectory, "MAIN.GST");
                if (!File.Exists(mainSavePath))
                {
                    continue;
                }

                results.Add(new SaveSlotInfo(
                    steamId,
                    Path.GetFileName(slotDirectory),
                    slotDirectory,
                    mainSavePath,
                    File.GetLastWriteTime(mainSavePath),
                    File.Exists(Path.Combine(slotDirectory, "DEFP_DLC1.MST")),
                    File.Exists(Path.Combine(slotDirectory, "DEFP_DLC2.MST"))));
            }
        }

        return results
            .OrderBy(slot => slot.SteamId, StringComparer.Ordinal)
            .ThenBy(slot => slot.SlotName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> RunningGameProcesses()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var processName in GameProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    found.Add($"{process.ProcessName} (PID {process.Id})");
                }
            }
        }

        return found.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static void EnsureGameIsClosed()
    {
        var running = RunningGameProcesses();
        if (running.Count > 0)
        {
            throw new InvalidOperationException(
                UiText.T($"EDF6が起動しています。セーブ変更・復元の前に終了してください。検出: {string.Join(", ", running)}", "EDF6 is running. Close the game before changing or restoring a save. " +
                $"Detected: {string.Join(", ", running)}"));
        }
    }
}

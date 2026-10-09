# EDF6 LocationSync v0.2.0 — Experimental

[日本語](README.md)

Receiver-side position-correction interval tweak for remote soldiers in Steam EDF6, loaded through EDFModLoader. The compatibility filename remains EDF6PositionSyncFix.dll.

**Live online testing has not been performed. This is not a guaranteed desync fix.**

## What changed

- Japanese / English settings GUI, with localized plugin logs after saving the language to the INI.
- Removed the forced fresh-sample flag from v0.1.0. Stale extrapolated targets are no longer treated as newly received samples.
- Replaced inline code detouring with one atomic, validated player-checker function-pointer replacement. No stolen instructions or executable trampoline.
- Requires the exact known EDF.dll SHA-256 and validates the function locations, checker and call site. Unknown builds and detected conflicts are refused.
- Pins the plugin before publishing its callback. Close the game before removing it; hot unloading is unsupported.

## Behavior and limitations

Changes the correction wait threshold from the observed game's default of 120 frames to 30 by default. The game's other conditions, including distance and fresh received information, must still be met. This is NOT a guaranteed snap every 30 frames. The fresh-sample flag, forced-correction flag and position values are preserved.

Only the verified remote-soldier checker call is targeted. No packet sending, bandwidth, latency, enemy/vehicle synchronization or save data changes. It cannot recover lost traffic or correct bad data from the sender. Shorter intervals may make snapping more visible. Disable it if it has no benefit or worsens movement.

## Supported build

Windows x64 / Steam EDF6. Only this EDF.dll SHA-256 is accepted:

`0D5092393A1B9D8F1B75B0C93D8A8F65615A36A92C46E40349C2B66C733D0916`

Locally inspected Steam build ID: 17055427. Future builds and other distributions are not automatically supported.

## Install and change language

1. Close EDF6.
2. Install an EDF6-compatible [EDFModLoader release](https://github.com/BlueAmulet/EDFModLoader/releases) using its instructions. The loader is not bundled.
3. Copy `Mods` and `EDF6LocationSync.Settings.exe` from this ZIP next to `EDF6.exe`. Preserve a customized old INI before overwriting it. Do not install the old and new DLL under different names at the same time.
4. Open the settings app. Choose 日本語 / English at the top right, select the game folder, adjust Enabled and the interval, then click Save settings. No in-game menu is added.
5. Start the game and check `Mods/EDF6PositionSyncFix.log` for "Loaded v0.2.0" (or its Japanese equivalent). Unsupported builds, conflicts and access errors require checking the log.

The settings app includes its .NET runtime. The native plugin works without the settings app. No auto-download, updater or telemetry is implemented.

## Manual INI

Edit `Mods/EDF6PositionSyncFix.ini` with the game closed:

```ini
[PositionSyncFix]
Enabled=1
IntervalFrames=30
Language=auto
```

Enabled: 1=on, 0=off. IntervalFrames: 10–120, default 30, clamped to this range. Language: ja / en / auto (Windows UI language, English for non-Japanese systems). The app changes its display language immediately; saved plugin settings and log language apply at the next game launch.

## Disable / uninstall

Close the game completely. Save Enabled=0 or remove `Mods/Plugins/EDF6PositionSyncFix.dll`. Optionally remove the settings EXE, INI and log. No original game file was modified.

## Testing and source

Automated checks cover signatures, atomic pointer replacement, isolated interval-field changes, SHA-256, unsupported DLL refusal, JA/EN logs and the settings GUI. Game startup, full missions, multiple PCs, high latency, all classes and other mods have not been tested. Use only in consenting private sessions.

Build from the repository root with `.\build-position-sync-fix.ps1 -GameDll "<path to EDF.dll>"`. Requires .NET 10 SDK, CMake, Ninja and x64 MinGW-w64. MSVC configuration exists but was not used for this release. The test-only fake EDF.dll is never included in the release.

Code, UI, translations and promotional illustrations were created using generative AI. Thumbnails are illustrations, not gameplay screenshots. Unofficial community mod. See the included license and third-party notices.

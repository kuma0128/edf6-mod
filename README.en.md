# EDF6 Weapon Unlocker v1.3.0

The Weapon Unlocker 1.3.0 and LocationSync 0.2.0 release ZIPs, source ZIP, and
SHA-256 checksums are archived in the
[private repository's Releases](https://github.com/kuma0128/edf6-mod/releases).

[日本語](README.md)

Standalone Windows x64 save tool for Steam EDF6. Unlock selected weapons and apply each weapon's normal maximum star values for all four classes. This is a save editor, not a runtime DLL mod.

## Japanese / English

Use the selector at the top right. Buttons, confirmations, result messages, DLC labels and application errors switch language. The choice is stored under `%LOCALAPPDATA%/EDF6WeaponUnlocker/language.json` and reused on launch. Windows-owned dialogs and system error messages may follow the Windows language.

The CLI supports `--lang ja` or `--lang en`. JSON field names remain stable.

## Use

1. Close EDF6 and allow Steam Cloud synchronization to finish. Keep the game and other save editors closed throughout the operation.
2. Extract the ZIP to a writable folder and launch `EDF6WeaponUnlocker.exe`.
3. Check the account/slot and DLC selection. Only use the manual DLC2 option if you own and have installed DLC2.
4. Click Unlock + MAX. A fresh preview runs before the confirmation. Confirm only the intended slot and range.
5. Check the completion log and refreshed counts, then launch EDF6 and inspect equipment.

No .NET installation is required for the self-contained package. No updater, telemetry or app network requests are implemented.

## Scope

- All four classes; a 1,564-entry per-weapon cap table; unused stat slots normalized to zero.
- Preserves existing ownership bits and special values above normal caps, except normalization of the specific v1.1.0/v1.1.1 bug pattern.
- Repairs invisible owned weapons affected by those older tool versions.
- DLC1 is included only when detected. DLC2 includes 110 weapons, with an explicit manual override for owners whose mission save is missing.
- Unowned paid, preorder and collaboration bonus items stay locked. Already-owned bonus items can be maxed.
- Four reserved records remain unchanged. No mission, armor, progression or Steam-license editing.

## Backups and safeguards

Backups are outside the game's Cloud save tree:
`%LOCALAPPDATA%/EDF6WeaponUnlocker/Backups/<SteamID>/<slot>/<timestamp-hash>/`.

Restore latest restores the newest recorded backup and first backs up the current save. The built-in restore requires a readable, valid current save. If the current file is corrupt, close the game and restore a verified MAIN.GST manually from a backup.

Checks include supported file size (56,572 bytes), MDB0 header, CRC32C, source SHA-256 after preview, permitted byte ranges and encryption round-trip. Backups are read back before committing. Immediately before replacing the save, the game-closed check and source-byte comparison run again. Malformed manifests are skipped. Write failures before replacement do not trigger an overwrite with an older copy. Verification failures after replacement attempt rollback only if the file still matches this tool's candidate.

Do not run simultaneous editors or let Cloud sync replace the file during editing. These checks cannot eliminate every external race or storage failure. Read the error and backup paths if anything fails. Keep independent backups.

Future formats with a different size/header/checksum are refused; matching these checks alone is not proof of compatibility with every future game version. Automated tests use synthetic saves and do not guarantee every in-game outcome.

## CLI

```powershell
tools/EDF6WeaponUnlocker.Cli.exe help --lang en
tools/EDF6WeaponUnlocker.Cli.exe inspect --lang en --slot "<saveslot00>"
tools/EDF6WeaponUnlocker.Cli.exe unlock --lang ja --slot "<saveslot00>"
tools/EDF6WeaponUnlocker.Cli.exe inspect --include-dlc2 --slot "<saveslot00>" --json
tools/EDF6WeaponUnlocker.Cli.exe backups --slot "<saveslot00>"
tools/EDF6WeaponUnlocker.Cli.exe restore-latest --slot "<saveslot00>"
```

Use `--backup-root <folder>` for a different backup location. `--yes` skips the interactive confirmation. Unknown, duplicate and missing-value arguments are rejected.

## Build / credits

.NET 10 SDK; `.\build-release.ps1 -SelfContained` runs tests and publishes GUI plus CLI. Runtime packs come from Microsoft's packages on NuGet.org. See ATTRIBUTION.md and licenses.

Unofficial tool; modified saves may affect progression, achievements and multiplayer fairness. Use offline or only with informed consent in private sessions. No game files or real saves are bundled. Code, UI, translations and promotional images were created using generative AI; thumbnails are not gameplay screenshots.

LocationSync is a separate download, not a dependency or a component of this Weapon Unlocker ZIP.

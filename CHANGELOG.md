# Changelog

## 1.3.0 — 2026-09-06

- Added immediate Japanese / English UI switching, persisted language selection, translated application errors and bilingual documentation.
- Added CLI `--lang ja|en` and strict argument validation to reject missing, duplicate or unknown arguments before save discovery/writing.
- Always inspect again before the final unlock confirmation. Refresh counts after applying/restoring only after the busy state is released.
- Recheck the game process and source bytes immediately before replacing the save; verify backups by reading them back. Do not roll back over an external change or after a pre-commit refusal.
- Skip malformed backup manifests safely. Removed the misleading blanket claim that any error means the save was untouched.
- Added isolated synthetic-save regression tests and JA/EN GUI/configuration rendering checks. Real saves and online sessions were not modified or used for testing.

## LocationSync 0.2.0 — 2026-09-06

- Added a standalone bilingual settings app and JA/EN plugin logs, selected with `Language=auto|ja|en`.
- Removed the forced fresh-network-sample flag; only adjust the correction interval while preserving the game's other correction conditions.
- Replaced inline instruction detours with an atomic, validated player-checker pointer hook. Pin the callback module to prevent unsafe unloading.
- Require the exact known EDF.dll SHA-256, expected RVAs and call-site/checker bytes. Added unsupported-DLL refusal and bilingual logging tests.
- Remains experimental: no live multiplayer, complete mission, all-class, high-latency or mod-interoperability validation.

## 1.2.0 — 2026-08-30

- Added explicit support for all 110 Additional Mission Pack 2: Visions of Malice weapon records (IDs 1451–1560) using the verified per-weapon maximum-star catalog.
- DLC2 can now be selected manually when `DEFP_DLC2.MST` has not yet been generated, with a clear ownership warning and no mission-file or Steam-license modification.
- Added CLI `--include-dlc2` support and regression coverage that proves exactly the DLC2 range is maxed while adjacent entitlement records remain untouched.

## 1.1.2 — 2026-08-27

- Fixed a critical save-field interpretation bug: the four-byte value before weapon stat stars is an ownership/state flag word, not an average-star value.
- Preserves existing flag bits while setting ownership bit 0, so ★MAX weapons remain visible in equipment lists.
- Detects and repairs the exact hidden-weapon records produced by v1.1.0/v1.1.1 (`flags = 8`, stat stars at 8 or higher), including previously owned entitlement weapons.
- Uses the verified 1,564-weapon Steam `WEAPONTABLE` maximum-star catalog instead of forcing every stat slot to 8; valid caps range from 0 through 10.
- Restores unused stat slots to zero and normalizes all records produced by the v1.1 ★8 bug.
- Added regression coverage for normal unlocks, existing flag preservation, and legacy visibility repair.

## 1.1.1 — 2026-08-26

- Fixed the ★MAX button remaining disabled before a manual safety recheck or after closing EDF6.
- The ★MAX button now runs the safety preview automatically when needed.
- When EDF6 is running, the button remains clickable and shows the exact blocking process while all save writes remain prohibited.

## 1.1.0 — 2026-08-26

- Added full ★MAX processing: average star and all eight weapon-stat stars are promoted to ★8.
- Added owned-entitlement detection: owned bonus weapons are maxed; unowned paid/pre-order/collaboration items remain locked.
- Starting weapons are now valid ★MAX targets while four "None" sentinels remain untouched.
- Added automatic rollback after any failed post-write verification.
- Added six dependency-free self-tests and a real-save-copy round-trip verification workflow.
- Updated GUI metrics and confirmation text for ★MAX behavior.

## 1.0.0 — 2026-08-26

- Initial Steam EDF6 weapon unlock implementation.
- Added save discovery, preview, backup, restore, CRC validation, and GUI/CLI front ends.

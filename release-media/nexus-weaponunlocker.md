# EDF6 Weapon Unlocker - Japanese and English

Version: 1.3.0
Suggested category: Utilities / Cheats (choose the closest category offered for EDF6).
Tags: AI-Generated Content; AI Media; Cheats (if offered).
Main file: EDF6WeaponUnlocker-v1.3.0-win-x64.zip
Requirement: Steam EDF6 save, Windows x64. The self-contained package includes .NET.
Thumbnail: weaponunlocker-thumbnail.png

## Short description

Bilingual Japanese/English save editor for Steam EDF6. Unlock selected weapons and apply per-weapon maximum stars for all classes, with previews and verified backups. Unowned bonus items stay locked; DLC2 override is for owners only.

## English description

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


## 日本語

Steam版『地球防衛軍6』の対象武器を取得済み・★MAXにするWindows x64セーブ編集ツールです。ゲーム実行中に読み込むDLL型MODではありません。

## 日本語 / English

画面右上で切り替えます。ボタン、確認文、結果、DLC表示、アプリのエラー表示が切り替わり、次回起動でも選択を保持します。設定先は `%LOCALAPPDATA%/EDF6WeaponUnlocker/language.json`。Windows標準ダイアログやOS由来のエラーはWindowsの言語に従う場合があります。CLIは `--lang ja` / `--lang en` に対応し、JSONキーは変更しません。

## 使い方

1. EDF6を完全に終了し、Steam Cloudの同期が終わるまで待ちます。処理中はゲームや他のセーブ編集ツールを起動しないでください。
2. ZIPを書き込み可能なフォルダーへ展開し、`EDF6WeaponUnlocker.exe` を起動します。.NET同梱版なので別途ランタイムを入れる必要はありません。
3. アカウント・対象スロット・DLC範囲を確認します。DLC2の手動指定はDLC2を所持・導入済みの場合のみ使用してください。
4. 「全武器を★MAX」を押します。直前の安全チェック後に確認画面が出るので、対象を確認して実行します。
5. 完了ログと再検証した件数を確認し、ゲームの装備画面を確認します。

## 対象と変更しないもの

- 全4兵科、1564武器の正規上限（★0～10）に対応。未使用性能スロットは0にします。
- 所持フラグの他のビットと上限超えの既存特殊値を保持します。ただし旧v1.1.0/v1.1.1固有の誤書き込みは正規化します。
- 旧版で非表示になった武器を検出して修復します。
- DLC1は検出時のみ対象化。DLC2は検出または所持者の手動指定で武器110件を対象化します。
- 所持済みの別売り・予約・コラボ特典は★MAX化できます。未所持品は開放しません。
- 4つの予約レコード、武器テーブルとCRC以外の進行度・アーマー・ミッション・Steamライセンスは編集しません。

## バックアップと保護処理

保存場所：
`%LOCALAPPDATA%/EDF6WeaponUnlocker/Backups/<SteamID>/<saveslotXX>/<日時-SHA256>/`

「直前へ復元」は最新の記録済みバックアップへ戻します。復元前のセーブも保存します。GUIでの復元は現在のセーブが正常に検証できる場合に限ります。現在のファイルが壊れている場合は、ゲームを終了し、検証済みバックアップのMAIN.GSTを手動で戻してください。

対応サイズ（56,572バイト）、MDB0ヘッダー、CRC32C、プレビュー後のSHA-256、変更範囲、暗号化・復号往復を検証します。バックアップも保存後に読み戻します。置換直前にゲーム終了と元ファイルの一致を再確認し、他の変更を検出したら上書きせず停止します。壊れたバックアップ記録は読み飛ばします。

置換前の失敗では古いセーブで上書きする「復元」を実行しません。置換後の検証失敗では、本ツールが保存したデータとまだ一致する場合のみ自動復元を試みます。外部の同時書き込みやストレージ障害を完全に防げるわけではありません。エラー時は状態とバックアップを確認してください。独立したバックアップも保持してください。

将来のゲーム更新でサイズ・ヘッダー・CRC条件が変われば拒否しますが、同じ条件を満たす未知の形式まで安全と保証はできません。テストは合成セーブで実施しており、すべての実機状態での無不具合を保証するものではありません。

## CLI・ビルド

```powershell
tools/EDF6WeaponUnlocker.Cli.exe help --lang ja
tools/EDF6WeaponUnlocker.Cli.exe inspect --lang en --slot "<saveslot00>"
tools/EDF6WeaponUnlocker.Cli.exe unlock --lang ja --slot "<saveslot00>"
tools/EDF6WeaponUnlocker.Cli.exe inspect --include-dlc2 --slot "<saveslot00>" --json
tools/EDF6WeaponUnlocker.Cli.exe restore-latest --slot "<saveslot00>"
```

`--backup-root <folder>` でバックアップ先、`--yes` で対話確認の省略を指定できます。不明な引数・重複・値の欠落は拒否します。

.NET 10 SDKを導入し `.\build-release.ps1 -SelfContained` で検証・配布ビルドします。Microsoftの公式ランタイムパックをNuGet.orgから取得します。利用アプリには自動更新、テレメトリ、独自の通信処理はありません。

非公式ツールです。改造セーブは進行・実績・公平性に影響するため、オフラインか参加者が合意したプライベート環境で使用してください。ゲームデータ・実際のセーブは配布しません。コード・UI・翻訳とサムネイルは生成AIを使用して作成しています。サムネイルはゲーム画面ではありません。参照元とライセンスはATTRIBUTION.mdとlicensesを参照してください。

LocationSyncは別の配布物で、このZIPには含めません。


## Image captions

- Thumbnail: AI-generated promotional illustration, not a gameplay screenshot. / AI生成のイメージ画像です。ゲーム画面ではありません。
- weaponunlocker-ja.png: Japanese interface using a synthetic save, not a user's save. / 合成セーブで表示した日本語画面です。実際のセーブではありません。
- weaponunlocker-en.png: English interface using a synthetic save, not a user's save. / 合成セーブで表示した英語画面です。実際のセーブではありません。

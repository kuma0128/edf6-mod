# EDF6 Weapon Unlocker v1.3.0

[English](README.en.md)

配布 ZIP（Weapon Unlocker 1.3.0 / LocationSync 0.2.0）、ソース ZIP、SHA-256 一覧は
[非公開リポジトリの Releases](https://github.com/kuma0128/edf6-mod/releases) に保存しています。

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

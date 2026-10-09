# 検証結果 / QA report

Date: 2026-09-06
Releases: Weapon Unlocker 1.3.0 / LocationSync 0.2.0
Environment: Windows x64, .NET 10, MinGW-w64 GCC 16.1.0, CMake/Ninja.

## 結論

日英対応・不具合修正後のリリースを作成しました。下記の自動テストと合成データによる画面確認は通過しています。実際のセーブ、ゲームのインストール先は変更していません。実オンラインセッションは検証していないため、LocationSyncは実験版です。「バグがない」「位置ずれが必ず直る」という保証ではありません。

Release builds and automated checks passed. Tests used synthetic saves and isolated configuration folders, not the user's live saves. The game installation was not modified. Live multiplayer remains untested; LocationSync is experimental.

## 修正した問題

### Weapon Unlocker

- 保存・復元後のプレビューがbusy状態のまま呼ばれて実行されない経路を修正。busy解除後に再検証します。
- 実行直前に常に新しいプレビューを取得し、古い件数や古いハッシュで確認させないようにしました。
- セーブ置換の直前に、ゲーム終了と元バイト列を再確認します。処理中の外部更新を検出した場合は上書きしません。
- 書き込み前の中止でも「ロールバック」として古いセーブを上書きする経路を防止しました。書き込み後に外部の変更を検出した場合も自動復元で上書きしません。
- バックアップを書いた後に読み戻して一致を確認します。
- null・短いハッシュ・壊れたJSONなどの不正なバックアップ情報をスキップし、一覧表示の例外を防止しました。
- エラー時に一律で「セーブは変更されていない」と表示する誤案内を削除しました。
- CLIの値の欠落（例: `unlock --slot`）・重複・不明な引数を、セーブの探索や編集前に拒否します。

### LocationSync

- 古い予測位置でも新しい受信情報があると扱っていたフラグ書き込みを削除。補正待機間隔のみ変更します。
- 実行コードの一部を複製するインラインフックを使用せず、整列した関数ポインター1個を原子的に交換します。呼び出し元も照合します。
- フック公開前にDLLをプロセス終了まで保持し、アンロードされたコードへの呼び出しを防止します。
- EDF.dllの完全なSHA-256一致と既知の関数位置・機械語を検証。未知ビルドは拒否します。
- 言語選択に応じたGUIとログ、既存INIのコメント・無関係なキーを残す設定保存を追加しました。

## 実施した確認

| Area | Result | Coverage |
| --- | --- | --- |
| Core + packaged CLI | 12/12 PASS | CRC32C、NIST AES-CTR、復号往復・改ざん検知、武器境界、1564件の上限表、所持フラグ・旧版修復、編集・復元、DLC2 110件、保存直前の外部更新・ゲーム起動、壊れたマニフェスト、日英設定、配布EXEでのCLI実行 |
| GUI/config test | PASS | 日英切替、言語保持、プレビュー完了、DLC2指定の維持、セーブ未検出時の書き込み無効化、INI往復・範囲補正・重複セクション拒否・未知キー保持 |
| Native CTest | 3/3 groups PASS | パターンスキャン、ポインター交換・競合拒否・復元、変更フィールド限定、SHA256既知ベクトル、EML6_Loadと版情報、EDF.dll不在・未知DLL拒否、日英ログ |
| Real EDF.dll read-only compatibility | PASS | SHA256、sync-update RVA 0x77AD70、player-branch RVA 0x59626A |
| JA/EN layout inspection | PASS at test size / 96 DPI | 両アプリの日本語・英語4画面を内部描画して目視確認。合成セーブ、例示パスを使用 |
| Release ZIP integrity | PASS | 各ZIP 10ファイル。すべて展開前のファイルとSHA256一致。実セーブ・ゲーム本体・テスト用EDF.dll・ログ・PDBなし |
| Clean source ZIP rebuild | PASS | ソースZIPから別フォルダーへ展開して.NETコア11件・GUIテスト・ネイティブ3群を再ビルドして実行 |
| Dependency/export review | PASS | プラグインはWindows/UCRT/BCryptのインポートとEML6_Loadを確認。追加のMinGWランタイムDLL不要 |

CLI検証は配布版EXEで日本語・英語help、JSON inspect、合成セーブのunlock/restore、キャンセル、値欠落・重複・不明・相反するDLC引数・不正言語を確認しました。GUIの最終確認ダイアログを自動クリックしたわけではありません。

ビルド時に警告・エラーはありませんでした。アプリ固有ソースにHTTPクライアント・ソケット・ダウンロード処理は見つかりませんでした。これはウイルス対策ソフトによる安全認定ではありません。署名されていない実行ファイルとしてWindowsやNexus Mods側で追加確認される場合があります。

## 未検証・残る制約

- ゲーム起動とミッション完走、実際の武器表示、全兵科・乗り物・複数PC・回線遅延・切断・他MODとの組み合わせ。
- 高DPI、複数モニター、特殊なフォント・Windowsテーマの全組み合わせ。
- ドライブ故障・ディスク満杯・権限異常の全パターン。外部プロセスとの競合を完全に排除するものではありません。ゲームとCloud同期と他の編集ツールを停止してください。
- Weapon UnlockerのGUI復元は、現在のMAIN.GSTが検証可能な場合のみ。壊れた現在ファイルは、ゲーム終了後に検証済みバックアップから手動復元してください。
- LocationSyncの動的アンロードは非対応。設定変更・削除前にゲームを完全に終了してください。
- Windows標準のダイアログ・システム由来のエラーまでアプリの言語選択に合わせて変わるとは限りません。
- Nexus Modsのログイン・投稿フォーム・実ファイルアップロード・サイト側検査・公開ページの確認は未実施。ブラウザーURL検出の安全チェックが停止したため、公開済みとしていません。

実オンライン検証では、MODなし／ありで同じ条件のプライベートミッションを比較し、ホスト・ゲスト双方、全兵科、復活・搭乗/降車、低/高遅延、30/60/120フレームを確認してください。悪化時は無効化します。

## 再実行コマンド

リポジトリルートから：

```powershell
.\build-release.ps1 -SelfContained
.\build-position-sync-fix.ps1 -GameDll "<EDF.dll>"
dotnet run --project tests\EDF6WeaponUnlocker.SelfTest -c Release -- "<配布版CLI EXE>"
dotnet run --project tests\EDF6WeaponUnlocker.UiSmokeTest -c Release -- "<画面画像の出力先>"
.\build-source-release.ps1
```

## 配布ZIP SHA-256

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| EDF6WeaponUnlocker-v1.3.0-win-x64.zip | 78536078 | 1F7660B6F7635C4F7913D212053737F37DE9FC23A0FD61BEE6EB62705ADE3AD1 |
| EDF6LocationSync-v0.2.0-win-x64.zip | 46344862 | 6D22F88BB121E28B0F30A17676700131A60876E897F9E7756E18C48459F99022 |
| EDF6-Bilingual-Source-2026-09-06.zip | 97390 | 4641466159F1CCAB7C20206E4EE63198812B3DE26D6D5209310C4CF4B6599283 |

主な実行ファイル：

- Weapon GUI: 909E1677672A79A6286AE7A63DD4F19FD90F4AF1589250791EDC221CA867C5D4
- Weapon CLI: 9DDFAD1CE7708F02C8EAB2FBEA1AAFAE11947A9F19F17D6683CC60AB55B8DD90
- LocationSync settings: CE6217E85185813FBA1D7E026C30FF746A3F8699C28EC87C2ABDB8496C3AB5B7
- LocationSync plugin: BC0BE8C33572E48642A278A905A5CF61377C494322437DE6BFEA38CFB59CD72E

旧リリースは元のartifactsフォルダーに残しています。新しいソースZIPには使用しなくなったv0.1のインラインフック実装を含めず、現行ビルドに必要なコードを収録しています。

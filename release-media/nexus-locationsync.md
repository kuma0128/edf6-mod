# EDF6 LocationSync - Japanese and English (Experimental)

Version: 0.2.0
Suggested category: Utilities / Gameplay (choose the closest category offered for EDF6).
Tags: AI-Generated Content; AI Media. Add only relevant game-specific tags available in the form.
Main file: EDF6LocationSync-v0.2.0-win-x64.zip
Requirement: EDFModLoader (EDF6-compatible release), Windows x64, exact supported Steam EDF.dll.
Thumbnail: locationsync-thumbnail.png

## Short description

Experimental receiver-side position-correction interval tweak for remote soldiers in Steam EDF6. Includes Japanese/English settings and logs. Exact build check; no packet or save editing. Live online results are unverified.

## English description

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


## 日本語

Steam版EDF6で、受信側のリモート兵士に対する位置補正の待機フレーム数を調整するEDFModLoaderプラグインです。旧名称・DLL名はEDF6PositionSyncFixのままです。

**オンライン実機検証は未実施です。位置ずれの解消を保証するものではありません。**

## v0.2.0の変更

- 日本語 / Englishを切り替えられる設定アプリを追加。INIに保存した言語でプラグインのログも切り替わります。
- 古い予測位置を「新しい受信位置」として扱っていたフラグの強制書き込みを削除しました。
- ゲームコードの先頭を書き換える方式を廃止し、検証したプレイヤー判定の関数ポインター1個だけを原子的に差し替えます。
- DLLのSHA-256、関数位置、判定関数、呼び出し元を照合します。競合・未対応の場合はフックを設定しません。
- フック公開前にプラグインをプロセス終了まで保持し、アンロード後の不正な呼び出しを防ぎます。動的アンロードには対応しません。

## 仕組みと制限

確認したビルドの受信側補正には待機フレーム数があり、既定の120を30に変更します。補正はゲーム本来の距離・受信状態などの条件も満たしたときだけ行われます。30フレームごとに必ず位置を合わせるMODではありません。新しい受信情報のフラグ、強制補正フラグ、座標は直接変更しません。

検証した呼び出し元のリモート兵士用判定だけが対象です。送信パケット、通信量、回線遅延、敵・乗り物の同期、セーブは変更しません。失われた通信や誤った送信座標は修復できません。短い間隔では引き戻しが目立つ場合があります。効果がない・悪化する場合は無効化してください。

## 対応版

Windows x64 / Steam EDF6。対応するEDF.dllのSHA-256は次の1種類のみです。

`0D5092393A1B9D8F1B75B0C93D8A8F65615A36A92C46E40349C2B66C733D0916`

ローカル検証対象のSteam build ID: 17055427。将来ビルド・他の配布版は自動対応しません。ゲーム更新後は対応状況を再確認してください。

## 導入・言語切り替え

1. EDF6を終了します。
2. [EDFModLoaderのEDF6対応リリース](https://github.com/BlueAmulet/EDFModLoader/releases)を、その説明に従って導入します。ローダー自体は本ZIPに含みません。
3. ZIP内の `Mods` フォルダーと `EDF6LocationSync.Settings.exe` を `EDF6.exe` の隣へコピーします。以前のINIを調整済みなら上書き前に控えてください。旧DLLと新DLLを別名で二重導入しないでください。
4. 設定アプリの右上で「日本語」または「English」を選び、対象フォルダー・有効化・間隔を確認して「設定を保存」を押します。ゲーム内メニューは追加しません。
5. ゲームを起動し、`Mods/EDF6PositionSyncFix.log` の「v0.2.0 読み込み完了」または「Loaded v0.2.0」を確認します。未知ビルド・競合・権限不足はログを確認してください。

設定アプリは.NETランタイム同梱です。プラグイン単体は設定アプリなしでも動作します。アプリはゲームファイルやローダーを自動ダウンロードしません。

## 手動設定

ゲーム終了中に `Mods/EDF6PositionSyncFix.ini` を編集してください。

```ini
[PositionSyncFix]
Enabled=1
IntervalFrames=30
Language=auto
```

- Enabled: 1=有効、0=無効。
- IntervalFrames: 10～120、既定30。範囲外は範囲内に丸めます。
- Language: ja=日本語、en=英語、auto=Windowsの表示言語（日本語以外は英語）。
- 保存した設定・ログ言語は次回のゲーム起動で反映されます。設定アプリの画面言語は即時切り替わります。

## 無効化・削除

ゲームを完全に終了してからEnabled=0で保存するか、`Mods/Plugins/EDF6PositionSyncFix.dll` を削除します。不要なら設定EXE・INI・ログも削除できます。ゲーム本体ファイルは改変していません。

## 検証範囲

ネイティブの署名検出、原子的ポインター交換、補正対象フィールドの限定、SHA-256、未対応DLL拒否、日本語・英語ログ、設定GUIのテストを実施しています。ゲームの起動、ミッション完走、複数PC・高遅延・全兵科・他MODとの共存は未検証です。実験版として参加者が合意したプライベート環境で試してください。

## ビルド

リポジトリルートで `.\build-position-sync-fix.ps1 -GameDll "<EDF.dllのパス>"`。
.NET 10 SDK、CMake、Ninja、x64 MinGW-w64が必要です（CMakeはMSVCにも対応する設定ですが、このリリースはMinGWで検証）。テスト用の偽EDF.dllは配布ZIPに含めません。

コード・UI・翻訳と掲載用イラストは生成AIを使用して作成しています。サムネイルはゲーム画面ではありません。非公式MODです。ライセンスと第三者表記は同梱ファイルを参照してください。


## Image captions

- Thumbnail: AI-generated promotional illustration, not a gameplay screenshot. / AI生成のイメージ画像です。ゲーム画面ではありません。
- locationsync-ja.png: Japanese settings screen rendered from the app using test data. / テストデータで表示した日本語設定画面。
- locationsync-en.png: English settings screen rendered from the app using test data. / テストデータで表示した英語設定画面。

# Nexus Mods掲載用セット

対象: [Earth Defense Force 6](https://www.nexusmods.com/games/earthdefenseforce6/mods)

## 状態

2026-09-06、ユーザーによるChrome拡張機能のファイルアクセス許可後、aeoux5のアカウントで両ページを公開しました。各ページの成功表示「Your mod is live」を確認済みです。**ただし両ZIPはNexus Modsの自動安全検査で隔離されており、ダウンロード可能な状態ではありません。**

| MOD | 公開ページ | 配布ファイル | 最終確認 |
| --- | --- | --- | --- |
| LocationSync v0.2.0 | https://www.nexusmods.com/earthdefenseforce6/mods/362 | file ID 2131 / 44.2 MB | Quarantined / Some suspicious files |
| Weapon Unlocker v1.3.0 | https://www.nexusmods.com/earthdefenseforce6/mods/363 | file ID 2132 / 74.9 MB | Quarantined / Some files not scanned |

- 各ページにメインZIP1個、サムネイル1枚、日英UI画像2枚を登録しました。画像タイトルにはAIイメージ／合成テスト画面であることを明記しました。
- 日英本文、配布バージョン、クレジット、同梱MITライセンスに合わせた権限表示を保存しました。LocationSyncにはEDFModLoaderの外部必須要件を登録しました。
- 両方にAI-Generated ContentとAI Mediaを保存しましたが、公開後のLocationSync最終確認ではこの2タグが表示されず、編集画面の本文にもこちらの操作ではない変更が見られました。競合する編集を上書きしていません。掲載ガイドラインに従ってAIタグを維持してください。
- 配布ページに示されたVirusTotalのSHA-256リンクは、それぞれローカルZIPのハッシュと一致しました。これだけで安全性が保証されるわけではありません。今回のターンではコード・配布ZIPを変更せず、ゲーム実機検証も行っていません。
- 新規ページやZIPを重複登録せず、今後は上記MOD ID / file IDを使用してください。

## 残作業：運営による隔離ファイルの審査

両ファイルのページには、自動安全検査による隔離と、support@nexusmods.comへソース公開URL・ビルド手順を添えて審査を依頼する案内が表示されています。[Nexus Modsの隔離案内](https://help.nexusmods.com/article/117-why-has-my-mod-been-quarantined)。個別の検出理由や誤検出かどうかは未確定です。安全検査の回避や同一ファイルの再アップロードはしていません。

ソースZIPはローカルに準備済みですが、このターンでは外部ソースリポジトリへの公開・サポートメール送信は実施していません。これらの新しい外部操作を進めるにはユーザーの指示が必要です。寄付／報酬プログラムへの参加設定は変更していません。

## LocationSync

- [配布ZIP](../artifacts/EDF6LocationSync-v0.2.0-win-x64.zip)
- [日英の掲載文・タイトル・ファイル情報](nexus-locationsync.md)
- [サムネイル](locationsync-thumbnail.png)
- [日本語UI](ui-previews/locationsync-ja.png) / [English UI](ui-previews/locationsync-en.png)

実験版の表記とオンライン実機未検証の注意書きを残してください。EDFModLoaderを必須要件として登録し、同梱していないことを明記します。

## Weapon Unlocker

- [配布ZIP](../artifacts/EDF6WeaponUnlocker-v1.3.0-win-x64.zip)
- [日英の掲載文・タイトル・ファイル情報](nexus-weaponunlocker.md)
- [サムネイル](weaponunlocker-thumbnail.png)
- [日本語UI](ui-previews/weaponunlocker-ja.png) / [English UI](ui-previews/weaponunlocker-en.png)

オフライン／合意したプライベート環境向け。未所持特典の開放はしないこと、DLC2手動指定は所有者向けであることを残してください。

## 共通資料

- [再ビルド可能なソースZIP](../artifacts/EDF6-Bilingual-Source-2026-09-06.zip)
- [検証レポート](QA-REPORT.md)
- [画像生成に使用したプロンプト](IMAGE-PROMPTS.md)

2つの機能は独立した配布物です。ソースZIPは任意の開発者向けファイルとして扱い、ゲームへのインストール用にしないでください。旧リリースZIPを誤って最新版として投稿しないでください。

公開時には使用アカウントの既存MODページがあるか確認して重複投稿を避けます。作者名・権限設定はアカウント／既存のMITライセンスに合わせます。テスト用偽EDF.dll、ゲーム本体、実セーブ、バックアップ、ログ、PDBは配布ZIPに含めません。

## AI表記

コード・UI・翻訳は生成AIで作成し、サムネイルと説明文にも生成AIを使用しています。AI Assistedのみではなく、AI-Generated Content と AI Media の該当タグを付ける掲載案です。[Nexus Mods公式投稿ガイドライン（2026-09-04更新）](https://help.nexusmods.com/article/28-file-submission-guidelines)

サムネイルはAI生成イメージ、UI画像は合成テストデータのアプリ表示である旨をキャプションに記載します。画像はゲームの効果を実証したものではありません。

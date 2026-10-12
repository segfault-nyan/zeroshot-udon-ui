# ZeroShot Udon UI

VRChat ワールド用の UI ツールキットです。ボタンやスライダーなどの部品を置くだけで、見た目の揃った UI を作れます。PC と Quest（Android）の両方で動きます。

フォントなどの既製品を除き、このツールキットは 100% 生成 AI（Claude）が作っています。

## 導入

VRChat Creator Companion（VCC）から導入します。

1. このリポジトリの導入ページを開きます。リポジトリのトップページの右側、「About」の欄にリンクがあります。
2. 「VCC に追加」を押します。VCC が開くので、追加を承認します。
3. VCC でプロジェクトの管理画面を開き、「ZeroShot Udon UI」を導入します。

使い方は、[導入手順書](Packages/com.helkya.zeroshotudonui/README.md)を読んでください。

## 文書

| 文書 | 内容 |
|---|---|
| [導入手順書](Packages/com.helkya.zeroshotudonui/README.md) | 最初の UI の置き方、操作の受け取り方、テーマ、日本語フォント |
| [部品リファレンス](Packages/com.helkya.zeroshotudonui/Documentation~/Reference.md) | 各部品の設定項目、イベント、スクリプトから使える操作 |
| [AI エージェント向け利用ガイド](Packages/com.helkya.zeroshotudonui/Documentation~/AIGuide.md) | AI にコードで UI を作らせるための手引き |
| [変更履歴](Packages/com.helkya.zeroshotudonui/CHANGELOG.md) | 版ごとの変更内容 |

## このリポジトリについて

このリポジトリは、ツールキットを開発するための Unity プロジェクトです。

| 場所 | 内容 |
|---|---|
| `Packages/com.helkya.zeroshotudonui/` | ツールキット本体。配布されるのは、このフォルダです |
| `Docs/DESIGN.md` | 設計書 |
| `Docs/RELEASE.md` | 公開の手順 |
| `CLAUDE.md` | 開発を担当する AI（Claude）への指示 |

開発に使うときは、VCC の「Add Existing Project」でこのフォルダを追加して開きます。VRChat SDK は、VCC が導入します。

## ライセンス

MIT ライセンスです。全文は [LICENSE.md](LICENSE.md) にあります。

フォントは同梱していません。フォントに関する第三者のライセンスは、[THIRD-PARTY-NOTICES.txt](Packages/com.helkya.zeroshotudonui/Editor/Fonts/THIRD-PARTY-NOTICES.txt) にまとめています。

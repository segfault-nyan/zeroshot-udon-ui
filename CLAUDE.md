# CLAUDE.md

## プロジェクト概要
AI（Claude）製の VRChat ワールド用 UI ツールキット「ZeroShot Udon UI」を制作する Unity プロジェクト。

## 作業ルール
- やりとりは日本語で簡潔に行う。
- 提案はすべてユーザーの判断を通し、許可が取れたものだけを実行する。
  ファイルの作成・編集・削除、コマンド実行、パッケージ変更は、事前に内容を提示して承認を得る。
- 調査のための読み取り（ファイル閲覧・検索）は許可なしで行ってよい。

## 設計書
- 設計は `Docs/DESIGN.md` で管理する。機能は ID（例：`CMP-01`）で指す。
- 実装や変更の前に設計書の該当項目を確認し、決定事項や状態が変わったら設計書も更新する。
- フォルダ構成と命名規則は設計書の8章に従う。

## 作業上の注意
- プレハブとデモシーンはコードから生成する（設計書 8.5）。プレハブやシーンを直接書き換えず、
  生成コードを直して Unity のメニューから作り直してもらう。
- Unity 上でのコンパイルと動作確認はユーザーに依頼する。確認が取れてからコミットする。
- `Packages/com.vrchat.*` は .gitignore の対象なので、検索ツールでは見つからない。
  SDK のコードを調べるときはシェルの grep を使う。
- ツールキット本体は `Packages/com.helkya.zeroshotudonui/` にある（VCC で配布するパッケージ）。
  `Assets/ZeroShotUdonUI/` は利用者ごとのファイル（取得したフォント、生成したデモシーンなど）の
  置き場所で、git では管理しない（設計書 8.1）。
- `Packages/com.helkya.zeroshotudonui/Runtime/Fonts/ZS_Font.asset` は、空の状態で git に記録し、
  手元の変更を git が無視する設定（skip-worktree）にしている。フォントは同梱しない方針のため
  （設計書 12章）。このファイルの変更が `git status` に出ないのは正常。

## 公開に関する注意
- リポジトリは GitHub で公開する。公開の手順は `Docs/RELEASE.md`。
- 利用者の GitHub のユーザー名とメールアドレスは、尋ねない。ファイルにも書かない。
  公開先の URL と作者の連絡先は、GitHub の自動処理が実行時に補う（`.github/scripts/release.py`）。
- ワールド ID（`wrld_…`）を含むシーンや、個人のアドレスを含む内容を、git に記録しない。
  `Assets/` の下は、`.gitkeep` 以外を記録しない方針（`.gitignore`）。
- 公開前の開発履歴は、リポジトリの外のバックアップ
  （`D:\world\ClaudeCreatedUIToolkit-history-backup.bundle`）にある。個人のアドレスとワールド ID を
  含むので、公開しない。

## 環境
- Unity 2022.3.22f1
- VRChat SDK Worlds 3.10.5（VCC 管理、UdonSharp 同梱）
- TextMeshPro 3.0.6 / uGUI

## 技術上の前提
- スクリプトは UdonSharp（`UdonSharpBehaviour`）で書く。Udon で使えない C# 機能・API に注意する。
- UI は uGUI + TextMeshPro で構築する（Unity の UI Toolkit / UIElements は VRChat ワールドでは動作しない）。
- `Library/`、`Packages/com.vrchat.*`（VRChat SDK）、`Assets/SerializedUdonPrograms/` は自動生成・外部管理のため手で編集しない。
- `.meta` ファイルは対応するアセットとセットで扱う。

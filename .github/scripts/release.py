#!/usr/bin/env python3
"""ZeroShot Udon UI の配布物を作る。GitHub Actions（.github/workflows/release.yml）から呼ばれる。

package: パッケージのフォルダから、VCC が読み込める zip を作る。
listing: 公開済みの各版の定義ファイルから、VCC が読む一覧（index.json）と導入ページ（index.html）を作る。

公開先の URL と作者の連絡先は、リポジトリのファイルには書かず、実行時に引数で受け取る。
標準ライブラリだけで動くので、手元でも試せる。
"""

import argparse
import hashlib
import html
import json
import os
import re
import sys
import urllib.parse
import zipfile
from pathlib import Path

PACKAGE_NAME = "com.helkya.zeroshotudonui"
PACKAGE_PATH = "Packages/" + PACKAGE_NAME
LISTING_ID = "com.helkya.repos.zeroshotudonui"

# 配布物に含めるフォントアセットは、中身が空でなければならない（フォントは再配布しない）。
# 空の状態は約 6KB、生成済みの状態は数 MB 以上になる。
FONT_ASSET_PATH = "Runtime/Fonts/ZS_Font.asset"
EMPTY_FONT_ASSET_MAX_BYTES = 100_000

NOREPLY_SUFFIX = "@users.noreply.github.com"
VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")


def fail(message):
    print("エラー: " + message, file=sys.stderr)
    sys.exit(1)


def build_package(args):
    package_dir = Path(args.root) / PACKAGE_PATH
    manifest = json.loads((package_dir / "package.json").read_text(encoding="utf-8"))

    version = manifest["version"]
    if not VERSION_PATTERN.match(version):
        fail("package.json の version が「1.2.3」の形ではありません: " + version)

    # 個人のアドレスを誤って公開しないよう、GitHub が発行する noreply のアドレスだけを受け付ける。
    if not args.author_email.endswith(NOREPLY_SUFFIX):
        fail("作者の連絡先が未設定か、GitHub の noreply のアドレスではありません。"
             "リポジトリの変数 AUTHOR_EMAIL に「…" + NOREPLY_SUFFIX + "」の形のアドレスを設定してください。")

    font_size = (package_dir / FONT_ASSET_PATH).stat().st_size
    if font_size > EMPTY_FONT_ASSET_MAX_BYTES:
        fail("フォントアセットが空ではありません（%d バイト）。フォントを含んだ状態では配布できません。" % font_size)

    zip_name = "%s-%s.zip" % (PACKAGE_NAME, version)
    repository_url = "https://github.com/" + args.repository
    file_url = "%s/blob/%s/%s/" % (repository_url, version, PACKAGE_PATH)

    manifest.setdefault("author", {})["email"] = args.author_email
    manifest["url"] = "%s/releases/download/%s/%s" % (repository_url, version, zip_name)
    manifest["changelogUrl"] = file_url + "CHANGELOG.md"
    manifest["documentationUrl"] = file_url + "README.md"
    manifest["licensesUrl"] = file_url + "LICENSE.md"

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    zip_path = output_dir / zip_name

    # zip の最上位に package.json が来る形にする。package.json だけは、上で書き足した内容に差し替える。
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(package_dir.rglob("*")):
            if not path.is_file():
                continue

            name = path.relative_to(package_dir).as_posix()
            if name == "package.json":
                archive.writestr(name, to_json(manifest))
            else:
                archive.write(path, name)

    # 一覧に載せる定義ファイル。zip の中身を確かめるためのハッシュ値を足す。
    manifest["zipSHA256"] = hashlib.sha256(zip_path.read_bytes()).hexdigest()
    (output_dir / "package.json").write_text(to_json(manifest), encoding="utf-8")
    (output_dir / "notes.md").write_text(read_changelog_section(package_dir / "CHANGELOG.md", version), encoding="utf-8")

    github_output = os.environ.get("GITHUB_OUTPUT")
    if github_output:
        with open(github_output, "a", encoding="utf-8") as output:
            output.write("version=%s\nzip=%s\n" % (version, zip_path.as_posix()))

    print("作成しました: %s（%d バイト）" % (zip_path, zip_path.stat().st_size))


def read_changelog_section(changelog_path, version):
    """変更履歴から、指定した版の節を取り出す。リリースの説明文に使う。"""
    lines = []
    found = False
    for line in changelog_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            if found:
                break
            found = line[3:].strip().startswith(version)
            continue
        if found:
            lines.append(line)

    if not found:
        fail("CHANGELOG.md に「## %s」の節がありません。" % version)

    return "\n".join(lines).strip() + "\n"


def build_listing(args):
    versions = {}
    for path in Path(args.manifests).rglob("package.json"):
        manifest = json.loads(path.read_text(encoding="utf-8"))
        if manifest.get("name") == PACKAGE_NAME:
            versions[manifest["version"]] = manifest

    ordered = dict(sorted(versions.items(), key=lambda item: version_key(item[0])))
    latest = list(ordered.values())[-1] if ordered else None
    listing_url = args.site_url.rstrip("/") + "/index.json"

    listing = {
        "name": "ZeroShot Udon UI",
        "id": LISTING_ID,
        "url": listing_url,
        "author": latest["author"]["name"] if latest else "helkya",
        "packages": {PACKAGE_NAME: {"versions": ordered}} if ordered else {},
    }

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "index.json").write_text(to_json(listing), encoding="utf-8")
    (output_dir / "index.html").write_text(render_page(listing_url, args.repository, ordered), encoding="utf-8")

    print("一覧を作成しました: %d 版（%s）" % (len(ordered), listing_url))


def version_key(version):
    """版番号を新旧の順に並べるための値。「1.0.0-beta」のような先行版は、同じ番号の正式版より前に置く。"""
    core, _, prerelease = version.partition("-")
    return [int(part) for part in core.split(".")], prerelease == "", prerelease


def render_page(listing_url, repository, versions):
    repository_url = "https://github.com/" + repository
    add_url = "vcc://vpm/addRepo?url=" + urllib.parse.quote(listing_url, safe="")

    rows = []
    for version, manifest in reversed(list(versions.items())):
        rows.append('<li><a href="%s">%s</a></li>' % (html.escape(manifest["url"], quote=True), html.escape(version)))
    version_list = "\n      ".join(rows) if rows else "<li>公開されている版は、まだありません。</li>"

    return PAGE_TEMPLATE.format(
        add_url=html.escape(add_url, quote=True),
        listing_url=html.escape(listing_url, quote=True),
        repository_url=html.escape(repository_url, quote=True),
        package_path=PACKAGE_PATH,
        version_list=version_list,
    )


def to_json(value):
    return json.dumps(value, ensure_ascii=False, indent=2) + "\n"


PAGE_TEMPLATE = """<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>ZeroShot Udon UI</title>
  <style>
    body {{ margin: 0; background: #171a21; color: #f2f5fa; font-family: system-ui, sans-serif; line-height: 1.7; }}
    main {{ max-width: 640px; margin: 0 auto; padding: 48px 16px; }}
    h1 {{ font-size: 28px; margin: 0 0 8px; }}
    h2 {{ font-size: 18px; margin: 40px 0 8px; }}
    p, li {{ color: #9ea8bd; }}
    a {{ color: #5c8cff; }}
    .button {{ display: inline-block; margin: 16px 0; padding: 12px 28px; border-radius: 999px;
               background: #5c8cff; color: #fff; font-weight: bold; text-decoration: none; }}
    code {{ display: block; padding: 12px; border-radius: 12px; background: #404554; color: #f2f5fa;
            overflow-wrap: anywhere; }}
  </style>
</head>
<body>
  <main>
    <h1>ZeroShot Udon UI</h1>
    <p>VRChat ワールド用の UI ツールキットです。PC と Quest の両方で動きます。</p>

    <a class="button" href="{add_url}">VCC に追加</a>
    <p>ボタンを押すと、VRChat Creator Companion（VCC）が開きます。追加した後、プロジェクトの管理画面で
      「ZeroShot Udon UI」を導入してください。</p>

    <h2>ボタンで追加できない場合</h2>
    <p>VCC の Settings &gt; Packages &gt; Add Repository に、次の URL を入力します。</p>
    <code>{listing_url}</code>

    <h2>使い方</h2>
    <ul>
      <li><a href="{repository_url}/blob/main/{package_path}/README.md">導入手順書</a></li>
      <li><a href="{repository_url}/blob/main/{package_path}/Documentation~/Reference.md">部品リファレンス</a></li>
      <li><a href="{repository_url}/blob/main/{package_path}/Documentation~/AIGuide.md">AI エージェント向け利用ガイド</a></li>
      <li><a href="{repository_url}">GitHub のリポジトリ</a></li>
    </ul>

    <h2>公開している版</h2>
    <ul>
      {version_list}
    </ul>
  </main>
</body>
</html>
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)

    package = commands.add_parser("package", help="パッケージの zip を作る")
    package.add_argument("--root", default=".", help="リポジトリの最上位のフォルダ")
    package.add_argument("--repository", required=True, help="GitHub のリポジトリ（所有者/名前）")
    package.add_argument("--author-email", default="", help="package.json に書き込む作者の連絡先")
    package.add_argument("--output", required=True, help="出力先のフォルダ")
    package.set_defaults(run=build_package)

    listing = commands.add_parser("listing", help="VCC が読む一覧と導入ページを作る")
    listing.add_argument("--manifests", required=True, help="各版の package.json を集めたフォルダ")
    listing.add_argument("--site-url", required=True, help="GitHub Pages の URL")
    listing.add_argument("--repository", required=True, help="GitHub のリポジトリ（所有者/名前）")
    listing.add_argument("--output", required=True, help="出力先のフォルダ")
    listing.set_defaults(run=build_listing)

    # 実行する環境の文字コードによらず、日本語のメッセージが化けないようにする。
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

    args = parser.parse_args()
    args.run(args)


if __name__ == "__main__":
    main()

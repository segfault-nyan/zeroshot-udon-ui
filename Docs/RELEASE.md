# 公開の手順

ZeroShot Udon UI を GitHub で公開し、VCC から導入できるようにするための手順です。公開の作業は、GitHub のアカウントを持つ人が行います。

## 仕組み

GitHub の Actions に、`Release` という自動処理があります（`.github/workflows/release.yml`）。手動で実行すると、次の処理が動きます。

1. `Packages/com.helkya.zeroshotudonui/package.json` の版番号で zip を作る。
2. GitHub のリリースを作り、zip を添付する。
3. 公開済みの全部の版から、VCC が読む一覧（`index.json`）と導入ページを作り、GitHub Pages に公開する。

公開先の URL と作者の連絡先は、リポジトリのファイルには書きません。URL は実行時に組み立て、連絡先はリポジトリの変数から読みます。そのため、リポジトリの名前や所有者が変わっても、ファイルを直す必要はありません。

## 最初に一度だけ行う設定

### 1. 連絡先のアドレスを用意する

1. GitHub の右上のアイコンから `Settings > Emails` を開く。
2. 「Keep my email addresses private」をオンにする。
3. 同じ画面に表示される、`数字+ユーザー名@users.noreply.github.com` の形のアドレスを控える。
4. 「Block command line pushes that expose my email」もオンにする。個人のアドレスが入ったコミットを、誤って公開するのを防げる。

### 2. 手元の git に、連絡先を設定する

プロジェクトのフォルダで、次のコマンドを実行する。アドレスは、手順 1 で控えたものに置き換える。

```
git config user.email "数字+ユーザー名@users.noreply.github.com"
```

以後のコミットの作者欄に、このアドレスが使われる。

### 3. リポジトリを作る

1. GitHub で `New repository` を開く。
2. 名前を決める（例：`zeroshot-udon-ui`）。公開範囲は `Public` にする。
3. 「Add a README file」などの追加の項目は、どれも選ばない。空のリポジトリを作る。

### 4. リポジトリを設定する

作ったリポジトリの `Settings` で、次の 2 つを設定する。

| 場所 | 設定 |
|---|---|
| `Secrets and variables > Actions > Variables` タブ > `New repository variable` | 名前 `AUTHOR_EMAIL`、値は手順 1 で控えたアドレス |
| `Pages > Build and deployment > Source` | `GitHub Actions` を選ぶ |

`AUTHOR_EMAIL` は、配布する `package.json` の作者の連絡先に書き込まれる。noreply の形でないアドレスを設定すると、自動処理はエラーで止まる。

### 5. 手元の内容を送る

プロジェクトのフォルダで、次のコマンドを実行する。URL は、作ったリポジトリのものに置き換える。

```
git remote add origin https://github.com/ユーザー名/リポジトリ名.git
git push -u origin main
```

### 6. 導入ページへのリンクを表示する

最初の公開（下の「版を公開する」）が済んだ後に行う。

1. リポジトリのトップページの右側、「About」の歯車を押す。
2. 「Use your GitHub Pages website」にチェックを入れて保存する。

## 版を公開する

### 公開の前に確かめること

| 確かめること | 理由 |
|---|---|
| `package.json` の `version` を、新しい版番号にした | 公開済みの版番号では、自動処理がエラーで止まる |
| `CHANGELOG.md` に、その版の節（`## 版番号（日付）`）がある | リリースの説明文に使う。無いとエラーで止まる |
| テーマ設定で「初期値に戻す」を押した状態のマテリアルが、git に記録されている | 記録されたマテリアルが、そのまま配布される |
| `git status` に、記録していない変更が無い | 配布されるのは、GitHub に送った内容 |
| 文書（`README.md`、`Documentation~`）が、変更に合っている | — |

フォントアセット（`ZS_Font.asset`）は、空の状態で git に記録されているので、操作は要らない。空でない場合は、自動処理がエラーで止まる。

### 公開する

1. 変更をコミットし、`git push` で GitHub に送る。
2. GitHub のリポジトリで `Actions` を開く。
3. 左の一覧から `Release` を選び、`Run workflow` を押す。「一覧と導入ページだけを作り直す」は、オフのままにする。
4. 処理が緑のチェックで終わるまで待つ（1〜2 分）。

### 公開の後に確かめること

1. リポジトリの `Releases` に、新しい版と zip がある。
2. 導入ページ（About のリンク）を開き、「公開している版」に新しい版がある。
3. VCC でプロジェクトを開き、新しい版に更新できる。

## うまくいかないとき

| 状況 | 対処 |
|---|---|
| リリースは作られたが、導入ページの公開で失敗した | `Run workflow` で「一覧と導入ページだけを作り直す」をオンにして、もう一度実行する |
| 公開した版に問題があった | 版番号を上げて、直した版を公開する。公開済みの版の中身は、差し替えない（VCC が古い内容を覚えていることがあるため） |
| 公開した版を取り下げたい | GitHub の `Releases` でその版を削除し、「一覧と導入ページだけを作り直す」をオンにして実行する |
| `Pages` の設定が見つからない、または公開に失敗する | `Settings > Pages` の `Source` が `GitHub Actions` になっているかを確かめる |

## 手元で試す

zip と一覧を作るスクリプト（`.github/scripts/release.py`）は、Python があれば手元でも動く。

```
python .github/scripts/release.py package --repository 所有者/名前 --author-email "1+name@users.noreply.github.com" --output dist
```

手元のフォントアセットが生成済みの状態だと、エラーで止まる。これは、フォントを含んだ zip を作らないための確認が働いているためで、正常な動作。

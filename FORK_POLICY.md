# フォーク運用方針（Upstream 競合回避）

フォーク元 [yohey-w/multi-agent-shogun](https://github.com/yohey-w/multi-agent-shogun) の更新を取り込みつつ、マージ競合を避けるための方針です。

## 基本ルール

### 1. フォーク元の「コマンド系」は編集しない

次のファイル・ディレクトリは **upstream 所有** とし、**当リポジトリでは編集しない** でください。

- `first_setup.sh`
- `install.bat`
- `setup.sh`
- `shutsujin_departure.sh`
- `instructions/` 配下（ashigaru.md, karo.md, shogun.md）
- `skills/skill-creator/SKILL.md`

**理由**: これらのファイルはフォーク元で頻繁に更新されるため、こちらで変更するとマージのたびに競合します。

### 2. CLI（WSL/tmux）は当フォークでは使わない

当リポジトリでは **Shogun.Avalonia アプリのみ** で完結し、CLI（shutsujin_departure.sh / WSL/tmux）での起動は行いません。upstream のコマンド系スクリプトは編集せず、upstream マージ時に本家版で上書きするだけにします。

### 3. 当リポジトリで触れる範囲（アプリ・マージ用）

| 対象 | 触れてよいこと |
|------|----------------|
| **読み取り** | `upstream_owned.txt` に含まれないファイル。例: `config/settings.yaml`、`config/projects.yaml`、`FORK_POLICY.md` など。 |
| **書き込み** | `upstream_owned.txt` に含まれないファイルだけ。例: `config/`、`queue/`、`dashboard.md`、`status/`、アプリが生成するデータ。本家スクリプトや `instructions/*` は編集しない。 |

### 4. 当リポジトリで編集してよいもの

- **設定**: `config/` のうち、`.gitignore` で無視していないもの（例: プロジェクト一覧のスキーマ用など）。実行時設定は `config/settings.yaml` 等でローカル管理。
- **マージ用**: `merge_upstream.sh`、`upstream_owned.txt`。
- **アプリ**: `Shogun.Avalonia/` 一式。フォーク元のコマンド・スクリプトは **参照・起動のみ** とし、スクリプト内容は変更しない。
- **ドキュメント**: `FORK_POLICY.md`、`README.md` のフォーク固有追記、`CLAUDE.md` の当リポジトリ用記述など。
- **その他**: `context/`, `templates/`, `queue/` の運用・スキーマは必要に応じて拡張してよいが、`instructions/` の文言は本家に合わせる場合は「upstream 取り込み」で上書きする前提にする。

### 5. upstream マージ手順（競合を避ける）

フォーク元の最新を取り込むときは、**upstream 所有ファイルは常に「本家の内容」で上書き** します。

```bash
# 1) マージ用スクリプトで一括取り込み（推奨）
./merge_upstream.sh

# 2) 手動で行う場合
git fetch upstream
git merge upstream/main --allow-unrelated-histories   # または通常の merge
# 競合したら、upstream 所有ファイルだけ本家版に戻す
git checkout upstream/main -- $(cat upstream_owned.txt)
git add -A && git commit -m "Merge upstream; restore upstream-owned files"
```

`upstream_owned.txt` に列挙したパスは、マージ後に `git checkout upstream/main -- <path>` で必ず本家版に戻してください。

## アプリ（Shogun.Avalonia）側の注意

- **参照してよいもの**: `config/projects.yaml`、`queue/`、`dashboard.md`、`status/` など、YAML/マークダウンで定義されているデータや設定。
- **編集・生成してよいもの**: 上記データの読み書き、当アプリ用の設定・UI。
- **触らないもの**: フォーク元リポジトリのコマンド用スクリプト（`shutsujin_departure.sh`、`first_setup.sh`、`install.bat`、`instructions/*` など）。これらは「参考・起動のみ」とし、内容の変更やパッチは当リポジトリでは行わない。

詳細は `Shogun.Avalonia/README.md` も参照してください。

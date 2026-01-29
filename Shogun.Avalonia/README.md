# Shogun.Avalonia

multi-agent-shogun 用の GUI アプリ（Avalonia）。**WSL と tmux を使わず**、フォーク元と同一の「将軍→家老→足軽」のロジックをアプリ内で完結する。

## 主な動作

1. **送信**: 入力欄で指示を入力して送信すると、`queue/shogun_to_karo.yaml` に cmd_xxx として追加され、アプリ内のオーケストレーターが家老（Claude API）→タスク分解→足軽（Claude API）→報告→`dashboard.md` 更新まで一括実行する。**WSL/tmux は不要**。
2. **ダッシュボード**: 「ダッシュボード」タブで `dashboard.md` の内容を表示。「更新」で再読み込み。送信後も自動で更新する。
3. **エージェント表示**: 「エージェント」タブで家老・足軽のキュー/タスク/報告を「更新」で反映する（家老＝shogun_to_karo の要約、足軽 N＝queue/tasks/ashigaruN.yaml と queue/reports/ashigaruN_report.yaml）。

## 設定

- **ワークスペースルート**: 設定画面の「ワークスペースルート」に、queue/dashboard/instructions の親フォルダ（multi-agent-shogun のフォルダ等）を指定する。空のときは config の親を参照する。
- **API キー・モデル**: Anthropic Claude の API キーとモデル名を設定する。未設定の場合はキューへの書き込みのみ行い、家老・足軽の実行は行わない。
- **memory/global_context.md**: ワークスペースルート直下の `memory/global_context.md` が存在する場合、家老・足軽のプロンプトに「システム全体の設定・殿の好み」として先頭に付与する。フォーク元のコンテキスト読み込み手順に準拠。
- **status/master_status.yaml**: 送信実行の開始・完了・失敗時に `status/master_status.yaml` を書き込む（フォーク元の shutsujin_departure.sh と同形式の全体進捗）。他ツールやスクリプトが参照する想定。

## フォーク運用上の注意

- **参照・起動してよいもの**: `config/projects.yaml`、`queue/`、`dashboard.md`、`status/`、`instructions/` など、YAML/マークダウンで定義されているデータや設定。これらは当アプリで読み書きしてよい。
- **編集しないこと**: フォーク元のコマンド用スクリプト（`shutsujin_departure.sh`、`first_setup.sh`、`install.bat`、`instructions/*` など）。これらは「参考」とし、スクリプト本体の変更やパッチは当方では行わない。フォーク元の更新で競合を避けるため（ルートの `FORK_POLICY.md` 参照）。

当方では CLI 起動は行わず、アプリ（Shogun.Avalonia）のみで完結する想定。

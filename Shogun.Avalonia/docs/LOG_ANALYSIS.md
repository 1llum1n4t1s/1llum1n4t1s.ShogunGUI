# ログ分析結果（Shogun.Avalonia.log）

## 概要

- **対象ログ**: `Shogun.Avalonia/bin/Debug/net10.0/Shogun.Avalonia.log`（約 1163 行）
- **期間**: 2026-02-02 19:31 ～ 20:10
- **主な流れ**: 起動 → 将軍・家老・足軽のジョブ実行 → 一部ジョブで API 使用量制限（exitCode 1）

---

## 正常に動作している点

| 項目 | 内容 |
|------|------|
| **起動** | Node.js / Claude Code CLI の確認・インストール、ログイン確認、8 ロールの常駐 Runner 起動まで問題なし。 |
| **RUNNER_CWD** | ドキュメントルート `C:\Users\szk\Documents\Shogun` が渡されている。 |
| **--add-dir** | 後半のジョブで `--add-dir C:\Users\szk\Documents\Shogun` が CLI に付与されている（963, 995, 1005, 1062, 1068 行付近）。 |
| **将軍・家老** | 指示の解決と YAML タスク分解が exitCode 0 で完了。 |
| **足軽 5 / 6** | queue タスク（shogun_to_karo 編集、config/settings.yaml パス統一）が exitCode 0 で完了。報告は scratchpad またはアプリ側で保存。 |

---

## 検出した不具合と対応

### 1. exitCode 1 のときに Success=True と表示されていた（修正済み）

**事象**: CLI が `You're out of extra usage · resets 9pm (Asia/Tokyo)` で終了（exitCode: 1）しているのに、ログに「足軽1 のジョブが完了しました。Success=True」と出ていた。

**原因**: RESULT 行の `exitCode: 1` をパースする際、`exitStr` が `"exitCode: 1,"` のとき「最後のカンマの後ろ」を数値として読んでおり、空文字になって exitCode が初期値の 1 のままだった。その結果、一部環境やログの取り方によって success が誤って true になる可能性があった。

**対応**: `ClaudeCodeProcessHost.ProcessOutputLine` で、`exitCode:` の直後の数値を明示的にパースするように変更した。これで exitCode 1 のときは常に success = false となり、「ジョブが完了しました。Success=False」と正しく記録される。

### 2. Claude API の extra usage 制限（アプリ外要因）

**事象**: 足軽 1～4 および家老（実行フェーズ・報告集約）のジョブで、CLI が次のメッセージで終了している。

```
You're out of extra usage · resets 9pm (Asia/Tokyo)
RESULT: exitCode: 1, output: "..."
```

**原因**: Claude Code の「extra usage」枠が枯渇しており、API 側の制限でジョブが実行できていない。

**対応**: アプリ側のコード変更では解消できない。  
- 翌日 21:00 (Asia/Tokyo) のリセットを待つ  
- または Claude / Cursor のプランで追加枠を増やす  

上記 1 の修正により、この場合は **Success=False** とログに出るようになり、原因が分かりやすくなる。

---

## その他のログ上のポイント

| 項目 | 説明 |
|------|------|
| **Runner 起動時の addDir** | ログ先頭の `[RUNNER] Started with env:` には `addDir=` が含まれていないセッションがある。常駐プロセスは起動時のスクリプトを保持するため、`--add-dir` を追加した後にアプリを再起動すると、新 Runner からは `addDir=C:\Users\szk\Documents\Shogun` が付く。 |
| **報告の保存先** | 足軽 5/6 は「新ファイル／報告書を scratchpad に保存」と報告。アプリ側の `WriteReportFromAshigaruResult` で `queue/reports/` にも書き込むため、報告内容はドキュメントルートの `queue/reports/` で確認できる。 |
| **config/queue の直接編集** | 足軽 5 は `queue/shogun_to_karo.yaml` の無効エントリ削除を「新ファイルを scratchpad に保存。本ファイルへの上書き権限確認後、元の位置に配置してください」と報告。`--add-dir` でドキュメントルートを渡しているため、権限が付与されていれば直接上書きも可能。 |

---

## まとめ

- **アプリ側の不具合**: exitCode のパースを修正し、exitCode 1 のときは常に Success=False となるようにした。
- **API 制限**: 「You're out of extra usage」は Claude の利用枠によるもので、リセット時刻の確認またはプラン見直しで対応する。
- **--add-dir**: ドキュメントルートは CLI に渡されており、config/queue の編集は設定次第で利用可能。

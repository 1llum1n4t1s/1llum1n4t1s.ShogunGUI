using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shogun.Avalonia.Util;

namespace Shogun.Avalonia.Services;

/// <summary>
/// アプリ内の Claude Code CLI を常駐プロセス経由で実行する。プロセスはアプリ起動時に起動し、アプリ終了時まで終了しない。
/// </summary>
public class ClaudeCodeRunService : IClaudeCodeRunService
{
    private const string KaroUserPrompt = @"queue/shogun_to_karo.yaml に新しい指示がある。確認して、以下のJSON形式で足軽タスク情報を出力せよ。

```json
{
  ""tasks"": [
    {
      ""ashigaru_id"": 1,
      ""description"": ""タスクの説明"",
      ""target_path"": ""対象ファイルパス（オプション）""
    }
  ]
}
```

注意: 複数の独立したタスクなら複数足軽に分散して並列実行させよ。JSONのみ出力し、余計な説明は不要。";
    
    private const string KaroExecutionPrompt = @"足軽からの報告書をすべて読んだ。確認せよ: queue/reports/ashigaru*_report.yaml

報告内容をまとめ、必要に応じて自分でコードを改修せよ。
1. 報告ファイルをすべて読む
2. 改修が必要なファイルを特定する
3. 必要に応じてファイルを Edit ツールで改修する
4. ビルドが成功することを確認する
5. 最終的なサマリーを出力する

改修内容を含めた最終報告を、以下のYAML形式で出力せよ:

---
modifications:
  - file: ""ファイルパス""
    description: ""改修内容""
result: ""成功/失敗""
summary: ""処理サマリー""
---";
    
    private const string KaroReportUserPrompt = "queue/reports/ の報告を確認し、dashboard.md の「戦果」を更新せよ。";

    private readonly IClaudeCodeProcessHost _processHost;
    private readonly IClaudeCodeSetupService _setupService;
    private readonly IShogunQueueService _queueService;
    private readonly IInstructionsLoader _instructionsLoader;

    /// <summary>サービスを生成する。</summary>
    public ClaudeCodeRunService(IClaudeCodeProcessHost processHost, IClaudeCodeSetupService setupService, IShogunQueueService queueService, IInstructionsLoader instructionsLoader)
    {
        _processHost = processHost;
        _setupService = setupService;
        _queueService = queueService;
        _instructionsLoader = instructionsLoader;
    }

    /// <inheritdoc />
    public async Task<bool> RunKaroAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? string.Empty;
        var result = await RunClaudeAsync(KaroUserPrompt, karoInstructions, progress, "家老", cancellationToken).ConfigureAwait(false);
        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            // 家老の出力（JSON）を解析して足軽タスクファイルを生成
            await GenerateAshigaruTasksFromKaroOutputAsync(result.Output, cancellationToken).ConfigureAwait(false);
        }
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<bool> RunKaroExecutionAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? string.Empty;
        var result = await RunClaudeAsync(KaroExecutionPrompt, karoInstructions, progress, "家老（実行フェーズ）", cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<bool> RunAshigaruAsync(int ashigaruIndex, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var max = _queueService.GetAshigaruCount();
        if (ashigaruIndex < 1 || ashigaruIndex > max)
        {
            progress?.Report($"足軽番号は 1～{max} の範囲で指定してください。");
            return false;
        }
        var ashigaruInstructions = _instructionsLoader.LoadAshigaruInstructions() ?? string.Empty;
        var userPrompt = $"queue/tasks/ashigaru{ashigaruIndex}.yaml に任務がある。確認して実行せよ。";
        var result = await RunClaudeAsync(userPrompt, ashigaruInstructions, progress, $"足軽{ashigaruIndex}", cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<bool> RunKaroReportAggregationAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? string.Empty;
        var result = await RunClaudeAsync(KaroReportUserPrompt, karoInstructions, progress, "家老（報告集約）", cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    /// <inheritdoc />
    public async Task<string> ResolveShogunCommandAsync(string userInput, string? projectId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var shogunInstructions = _instructionsLoader.LoadShogunInstructions() ?? string.Empty;
        var userPrompt = $"殿の指示: {userInput}\nプロジェクトID: {projectId ?? "未指定"}\n\n上記を解析し、家老への具体的な指示文（1つのテキストブロック）を生成せよ。";
        
        var result = await RunClaudeAsync(userPrompt, shogunInstructions, progress, "将軍", cancellationToken).ConfigureAwait(false);
        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            return result.Output;
        }
        return userInput;
    }

    /// <summary>常駐プロセスにジョブを送り、結果を返す。プロセスは終了しない。</summary>
    private async Task<(bool Success, string Output)> RunClaudeAsync(string userPrompt, string systemPromptContent, IProgress<string>? progress, string roleLabel, CancellationToken cancellationToken)
    {
        var repoRoot = _queueService.GetRepoRoot();
        if (string.IsNullOrEmpty(repoRoot) || !Directory.Exists(repoRoot))
        {
            progress?.Report("ワークスペースルートが設定されていません。設定で指定してください。");
            return (false, string.Empty);
        }
        string? promptFile = null;
        try
        {
            progress?.Report($"{roleLabel}（常駐プロセス）にジョブを送信中…");
            Logger.Log($"{roleLabel} のジョブを送信します。UserPrompt='{userPrompt}'", LogLevel.Info);
            promptFile = Path.Combine(Path.GetTempPath(), "shogun-prompt-" + Guid.NewGuid().ToString("N")[..8] + ".md");
            await File.WriteAllTextAsync(promptFile, systemPromptContent, cancellationToken).ConfigureAwait(false);
            Logger.Log($"システムプロンプトファイルを生成しました: {promptFile}", LogLevel.Debug);
            var (success, outputStr) = await _processHost.RunJobAsync(roleLabel, userPrompt, promptFile, progress, cancellationToken).ConfigureAwait(false);
            Logger.Log($"{roleLabel} のジョブが完了しました。Success={success}", LogLevel.Info);
            if (!success)
                progress?.Report($"{roleLabel}の実行が失敗しました。");
            else
                progress?.Report($"{roleLabel}の実行が完了しました。");
            return (success, outputStr);
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: {ex.Message}");
            return (false, string.Empty);
        }
        finally
        {
            if (!string.IsNullOrEmpty(promptFile) && File.Exists(promptFile))
            {
                try { File.Delete(promptFile); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>家老の JSON 出力から足軽タスクを生成する。</summary>
    private async Task GenerateAshigaruTasksFromKaroOutputAsync(string karoJson, CancellationToken cancellationToken)
    {
        try
        {
            var repoRoot = _queueService.GetRepoRoot();
            
            // markdown コードブロック（```json ... ```）を除去
            var jsonText = karoJson;
            if (jsonText.StartsWith("```json", StringComparison.Ordinal))
                jsonText = jsonText.Substring(7);
            if (jsonText.StartsWith("```", StringComparison.Ordinal))
                jsonText = jsonText.Substring(3);
            if (jsonText.EndsWith("```", StringComparison.Ordinal))
                jsonText = jsonText.Substring(0, jsonText.Length - 3);
            jsonText = jsonText.Trim();
            
            var json = System.Text.Json.JsonDocument.Parse(jsonText);
            var root = json.RootElement;
            if (!root.TryGetProperty("tasks", out var tasksElement) || tasksElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return;
            
            foreach (var taskElem in tasksElement.EnumerateArray())
            {
                if (!taskElem.TryGetProperty("ashigaru_id", out var idElem))
                    continue;
                var ashigaruId = idElem.GetInt32();
                var description = taskElem.TryGetProperty("description", out var descElem) ? descElem.GetString() ?? "" : "";
                var targetPath = taskElem.TryGetProperty("target_path", out var pathElem) ? pathElem.GetString() ?? "" : "";
                var yaml = $"""
task:
  task_id: task_{ashigaruId}_{DateTime.Now:HHmmss}
  description: "{description}"
  {(string.IsNullOrEmpty(targetPath) ? "" : $"target_path: {targetPath}")}
  status: assigned
  timestamp: "{DateTime.UtcNow:O}"
""";
                var taskFilePath = Path.Combine(repoRoot, "queue", "tasks", $"ashigaru{ashigaruId}.yaml");
                await File.WriteAllTextAsync(taskFilePath, yaml, cancellationToken).ConfigureAwait(false);
                Logger.Log($"足軽{ashigaruId}タスクファイルを生成しました: {taskFilePath}", LogLevel.Debug);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"足軽タスク生成エラー: {ex.Message}", LogLevel.Warning);
        }
    }
}

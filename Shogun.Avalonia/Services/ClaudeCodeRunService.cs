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
    private const string KaroUserPrompt = "queue/shogun_to_karo.yaml に新しい指示がある。確認して実行せよ。";
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
}

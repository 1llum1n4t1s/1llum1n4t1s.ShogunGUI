using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shogun.Avalonia.Util;

namespace Shogun.Avalonia.Services;

/// <summary>
/// アプリ内の Claude Code CLI を起動し、家老としてキュー処理を実行する。
/// </summary>
public class ClaudeCodeRunService : IClaudeCodeRunService
{
    private const string KaroUserPrompt = "queue/shogun_to_karo.yaml に新しい指示がある。確認して実行せよ。";
    private const string KaroReportUserPrompt = "queue/reports/ の報告を確認し、dashboard.md の「戦果」を更新せよ。";

    private readonly IClaudeCodeSetupService _setupService;
    private readonly IShogunQueueService _queueService;
    private readonly IInstructionsLoader _instructionsLoader;

    /// <summary>サービスを生成する。</summary>
    public ClaudeCodeRunService(IClaudeCodeSetupService setupService, IShogunQueueService queueService, IInstructionsLoader instructionsLoader)
    {
        _setupService = setupService;
        _queueService = queueService;
        _instructionsLoader = instructionsLoader;
    }

    /// <inheritdoc />
    public Task<bool> RunKaroAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? string.Empty;
        return RunClaudeAsync(KaroUserPrompt, karoInstructions, progress, "家老", cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RunAshigaruAsync(int ashigaruIndex, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var max = _queueService.GetAshigaruCount();
        if (ashigaruIndex < 1 || ashigaruIndex > max)
        {
            progress?.Report($"足軽番号は 1～{max} の範囲で指定してください。");
            return Task.FromResult(false);
        }
        var ashigaruInstructions = _instructionsLoader.LoadAshigaruInstructions() ?? string.Empty;
        var userPrompt = $"queue/tasks/ashigaru{ashigaruIndex}.yaml に任務がある。確認して実行せよ。";
        return RunClaudeAsync(userPrompt, ashigaruInstructions, progress, $"足軽{ashigaruIndex}", cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RunKaroReportAggregationAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? string.Empty;
        return RunClaudeAsync(KaroReportUserPrompt, karoInstructions, progress, "家老（報告集約）", cancellationToken);
    }

    /// <summary>Claude Code CLI を指定プロンプト・システムプロンプトで起動する。</summary>
    private async Task<bool> RunClaudeAsync(string userPrompt, string systemPromptContent, IProgress<string>? progress, string roleLabel, CancellationToken cancellationToken)
    {
        var claudePath = _setupService.GetClaudeExecutablePath();
        if (string.IsNullOrEmpty(claudePath) || !File.Exists(claudePath))
        {
            progress?.Report("Claude Code CLI がインストールされていません。設定でインストールしてください。");
            return false;
        }
        var repoRoot = _queueService.GetRepoRoot();
        if (string.IsNullOrEmpty(repoRoot) || !Directory.Exists(repoRoot))
        {
            progress?.Report("ワークスペースルートが設定されていません。設定で指定してください。");
            return false;
        }
        string? promptFile = null;
        try
        {
            progress?.Report($"{roleLabel}（Claude Code）を起動中…");
            Logger.Log($"{roleLabel} の実行を開始します。UserPrompt='{userPrompt}'", LogLevel.Info);
            promptFile = Path.Combine(Path.GetTempPath(), "shogun-prompt-" + Guid.NewGuid().ToString("N")[..8] + ".md");
            await File.WriteAllTextAsync(promptFile, systemPromptContent, cancellationToken).ConfigureAwait(false);
            Logger.Log($"システムプロンプトファイルを生成しました: {promptFile}", LogLevel.Debug);
            
            var args = $"-p \"{userPrompt.Replace("\"", "\\\"")}\" --system-prompt-file \"{promptFile}\"";
            Logger.Log($"実行コマンド: {claudePath} {args}", LogLevel.Debug);
            Logger.Log($"作業ディレクトリ: {repoRoot}", LogLevel.Debug);

            using var proc = new Process();
            proc.StartInfo.FileName = claudePath;
            proc.StartInfo.Arguments = args;
            proc.StartInfo.WorkingDirectory = repoRoot;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            
            Logger.Log($"{roleLabel} プロセスを起動します...", LogLevel.Info);
            proc.Start();
            
            // 出力を非同期で読み取る（ハング対策）
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            Logger.Log($"{roleLabel} プロセスが終了しました。ExitCode: {proc.ExitCode}", LogLevel.Info);
            if (!string.IsNullOrWhiteSpace(stdout))
                Logger.Log($"{roleLabel} 標準出力: {stdout.Trim()}", LogLevel.Debug);

            if (proc.ExitCode != 0)
            {
                Logger.Log($"{roleLabel} 標準エラー出力: {stderr.Trim()}", LogLevel.Warning);
                progress?.Report($"{roleLabel}の実行が終了コード {proc.ExitCode} で終了しました。{(string.IsNullOrWhiteSpace(stderr) ? "" : " " + stderr.Trim())}");
                return false;
            }
            progress?.Report($"{roleLabel}の実行が完了しました。");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"エラー: {ex.Message}");
            return false;
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

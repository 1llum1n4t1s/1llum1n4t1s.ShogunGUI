using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shogun.Avalonia.Services;

/// <summary>
/// アプリ内の Claude Code CLI を起動し、家老・足軽としてキュー処理を実行するサービス。
/// </summary>
public interface IClaudeCodeRunService
{
    /// <summary>家老として Claude Code CLI を起動する。queue/shogun_to_karo.yaml を読んで足軽へ割り当てる指示を実行する。作業ディレクトリは RepoRoot。</summary>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功したら true。CLI 未インストール・起動失敗・終了コード非0 のときは false。</returns>
    Task<bool> RunKaroAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>家老として報告を確認し、必要なら次タスクを割り当てる。queue/reports/ を読み dashboard を更新し、追加タスクがあれば YAML で出力する。追加タスクがあれば足軽タスクファイルを生成する。</summary>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功可否と、追加タスクを割り当てたか（true のときは queue/tasks に新規タスクあり）。</returns>
    Task<(bool Success, bool HasMoreTasks)> RunKaroReportCheckAndMaybeAssignAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>足軽 N に /clear を送る（本家の /clear プロトコル。次タスク投入前にコンテキストをクリアする）。短いタイムアウトで完了を待つ。</summary>
    /// <param name="ashigaruIndex">足軽番号（1～GetAshigaruCount()）。</param>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>送信完了またはタイムアウトで true。プロセス不在等で false。</returns>
    Task<bool> SendClearToAshigaruAsync(int ashigaruIndex, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>足軽 N として Claude Code CLI を起動する。queue/tasks/ashigaru{N}.yaml の任務を実行し、queue/reports/ashigaru{N}_report.yaml に報告する。projectId が指定されていれば CWD をプロジェクトルートにする。</summary>
    /// <param name="ashigaruIndex">足軽番号（1～GetAshigaruCount()）。</param>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <param name="projectId">プロジェクトID。指定時は config/projects.yaml の当該 path を CWD（プロジェクトルート）に使用。</param>
    /// <returns>成功したら true。</returns>
    Task<bool> RunAshigaruAsync(int ashigaruIndex, IProgress<string>? progress = null, CancellationToken cancellationToken = default, string? projectId = null);

    /// <summary>家老として報告集約を実行する。queue/reports/ をスキャンし、dashboard.md の「戦果」を更新する。</summary>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功したら true。</returns>
    Task<bool> RunKaroReportAggregationAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>家老として足軽の報告をもとにコード改修を実行する。queue/reports/ の報告を読み、必要に応じてファイルを Edit ツールで改修する。</summary>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功したら true。</returns>
    Task<bool> RunKaroExecutionAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>将軍として Claude Code CLI を起動し、殿の指示を家老への具体的な指示文に解決する。</summary>
    /// <param name="userInput">殿（ユーザー）の入力。</param>
    /// <param name="projectId">プロジェクトID。</param>
    /// <param name="progress">進捗メッセージ（任意）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>解決された指示文。失敗時は userInput をそのまま返すか、エラーメッセージを返す。</returns>
    Task<string> ResolveShogunCommandAsync(string userInput, string? projectId, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

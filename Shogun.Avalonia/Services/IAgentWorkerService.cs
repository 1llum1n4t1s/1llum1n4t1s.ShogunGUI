using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 将軍・家老・足軽のワーカーをアプリ起動時に起動し、殿のメッセージをジョブとして投入するサービス。
/// </summary>
public interface IAgentWorkerService
{
    /// <summary>将軍・家老・足軽のワーカーと常駐プロセスを起動する。アプリ起動時に1回呼ぶ。</summary>
    /// <param name="onProcessReady">プロセス起動完了時のコールバック（roleLabel, message）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    Task StartAllAsync(Func<string, string, Task>? onProcessReady = null, CancellationToken cancellationToken = default);

    /// <summary>全ワーカーと常駐プロセスを終了する。アプリ終了時に呼ぶ。</summary>
    void StopAll();

    /// <summary>足軽の「1回以上ジョブ実行済み」状態をリセットする。「新規で送信」時に呼び、次ジョブで /clear を送らないようにする。</summary>
    void ResetAshigaruRunState();

    /// <summary>殿のメッセージを将軍に投げ、完了時に結果メッセージを返す。</summary>
    /// <param name="userInput">殿の入力。</param>
    /// <param name="projectId">プロジェクトID。</param>
    /// <param name="shogunProgress">将軍ペイン用の進捗。</param>
    /// <param name="karoProgress">家老ペイン用の進捗。</param>
    /// <param name="reportProgress">家老（報告集約）ペイン用の進捗。</param>
    /// <param name="ashigaruProgressFor">足軽番号を受け取りそのペイン用の進捗を返す関数。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>完了時の結果メッセージ。</returns>
    Task<string> SubmitMessageAsync(
        string userInput,
        string? projectId,
        IProgress<string> shogunProgress,
        IProgress<string> karoProgress,
        IProgress<string> reportProgress,
        Func<int, IProgress<string>> ashigaruProgressFor,
        CancellationToken cancellationToken = default);
}

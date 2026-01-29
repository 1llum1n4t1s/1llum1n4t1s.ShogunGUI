using System.Threading;
using System.Threading.Tasks;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 将軍→家老→足軽のフローをアプリ内で完結させるオーケストレーター（WSL/tmux 不要）。
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>指定したコマンド ID について、家老によるタスク分配→足軽実行→報告→dashboard 更新を行う。</summary>
    /// <param name="commandId">キューに追加したコマンド ID（例: cmd_001）。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功時は完了メッセージ、失敗時はエラーメッセージ。</returns>
    Task<string> RunAsync(string commandId, CancellationToken cancellationToken = default);
}

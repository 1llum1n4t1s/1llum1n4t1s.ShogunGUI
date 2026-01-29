using System.IO;

namespace Shogun.Avalonia.Services;

/// <summary>
/// フォーク元の instructions/*.md をワークスペースルートから読み込むサービス。
/// </summary>
public class InstructionsLoader : IInstructionsLoader
{
    private readonly IShogunQueueService _queueService;

    /// <summary>サービスを生成する。</summary>
    public InstructionsLoader(IShogunQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <inheritdoc />
    public string? LoadKaroInstructions()
    {
        var path = Path.Combine(_queueService.GetRepoRoot(), "instructions", "karo.md");
        return File.Exists(path) ? File.ReadAllText(path) : GetKaroFallback();
    }

    /// <inheritdoc />
    public string? LoadAshigaruInstructions()
    {
        var path = Path.Combine(_queueService.GetRepoRoot(), "instructions", "ashigaru.md");
        return File.Exists(path) ? File.ReadAllText(path) : GetAshigaruFallback();
    }

    /// <inheritdoc />
    public string? LoadGlobalContext()
    {
        var path = Path.Combine(_queueService.GetRepoRoot(), "memory", "global_context.md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string GetKaroFallback()
    {
        return @"汝は家老なり。Shogun（将軍）からの指示を受け、Ashigaru（足軽）に任務を振り分けよ。
自ら手を動かすことなく、配下の管理に徹せよ。
将軍の指示を「目的」として受け取り、最適な実行計画を自ら設計し、足軽1～8に割り当てよ。
出力は必ず指定のJSON形式のみ返せ。";
    }

    private static string GetAshigaruFallback()
    {
        return @"汝は足軽なり。Karo（家老）からの指示を受け、実際の作業を行う実働部隊である。
与えられた任務を忠実に遂行し、完了したら報告せよ。
出力は必ず指定のJSON形式のみ返せ。";
    }
}

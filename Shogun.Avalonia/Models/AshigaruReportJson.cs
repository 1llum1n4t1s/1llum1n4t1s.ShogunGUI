namespace Shogun.Avalonia.Models;

/// <summary>
/// 足軽が返す報告（YAML パース用）。フォーク元の queue/reports/ashigaru{N}_report.yaml 形式に合わせる。
/// </summary>
public partial class AshigaruReportJson
{
    /// <summary>タスク ID。</summary>
    public string? TaskId { get; set; }

    /// <summary>発令日時（ISO8601）。</summary>
    public string? Timestamp { get; set; }

    /// <summary>状態（done 等）。</summary>
    public string? Status { get; set; }

    /// <summary>結果要約。</summary>
    public string? Result { get; set; }

    /// <summary>スキル化候補を発見したか（必須）。</summary>
    public bool SkillCandidateFound { get; set; }

    /// <summary>スキル名（found が true の場合）。</summary>
    public string? SkillCandidateName { get; set; }

    /// <summary>スキル説明（found が true の場合）。</summary>
    public string? SkillCandidateDescription { get; set; }

    /// <summary>スキル化理由（found が true の場合）。</summary>
    public string? SkillCandidateReason { get; set; }
}

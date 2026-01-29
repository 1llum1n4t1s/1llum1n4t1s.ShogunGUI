namespace Shogun.Avalonia.Models;

/// <summary>
/// アプリケーション設定モデル。
/// 言語・足軽人数は本家スクリプト（shutsujin_departure.sh）が config/settings.yaml を読むため、
/// 当アプリでは編集しない（FORK_POLICY: 本家スクリプトを触らない方針）。skill.*, screenshot.*, logging.* のみ。
/// </summary>
public class AppSettings
{
    /// <summary>スキル保存先（config: skill.save_path）。生成スキルの保存先。</summary>
    public string SkillSavePath { get; set; } = string.Empty;

    /// <summary>ローカルスキル保存先（config: skill.local_path）。プロジェクト専用スキル。</summary>
    public string SkillLocalPath { get; set; } = string.Empty;

    /// <summary>スクリーンショット保存先（config: screenshot.path）。</summary>
    public string ScreenshotPath { get; set; } = string.Empty;

    /// <summary>ログレベル（config: logging.level）。debug, info, warn, error。</summary>
    public string LogLevel { get; set; } = "info";

    /// <summary>ログ保存先（config: logging.path）。</summary>
    public string LogPath { get; set; } = string.Empty;

    /// <summary>AI API キー（Anthropic Claude 用。config にはなし）。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>AI モデル名（Claude。例: claude-sonnet-4-20250514）。config にはなし。</summary>
    public string ModelName { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>API エンドポイント（Claude では未使用。将来用に保持）。config にはなし。</summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>ワークスペースルート（queue/dashboard/instructions の親フォルダ）。空のときは config の親を使用。</summary>
    public string RepoRoot { get; set; } = string.Empty;
}

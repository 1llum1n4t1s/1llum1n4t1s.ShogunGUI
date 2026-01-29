namespace Shogun.Avalonia;

/// <summary>
/// アプリ内で参照する定数（config/settings.yaml の ashigaru_count に相当する値はコアが 8 固定のため定数で保持）。
/// </summary>
public static class AppConstants
{
    /// <summary>足軽の人数（将軍・家老以外の実働エージェント数）。UI のペイン数・表示名に使用。</summary>
    public const int AshigaruCount = 8;
}

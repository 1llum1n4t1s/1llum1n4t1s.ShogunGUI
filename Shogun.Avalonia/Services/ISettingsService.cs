using Shogun.Avalonia.Models;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 設定の読み込み・保存を行うサービス。
/// </summary>
public interface ISettingsService
{
    /// <summary>現在の設定を取得する。</summary>
    AppSettings Get();

    /// <summary>設定を保存する。</summary>
    void Save(AppSettings settings);
}

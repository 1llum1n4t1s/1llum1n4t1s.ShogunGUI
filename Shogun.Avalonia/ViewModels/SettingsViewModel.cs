using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shogun.Avalonia.Models;
using Shogun.Avalonia.Services;

namespace Shogun.Avalonia.ViewModels;

/// <summary>
/// 設定画面の ViewModel。
/// 言語・足軽人数は本家スクリプトが config/settings.yaml を読むため当アプリでは変更不可（FORK_POLICY）。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly Action? _onClose;

    /// <summary>ログレベルの選択肢（logging.level）。XAML バインディング用。</summary>
    public IReadOnlyList<string> LogLevelOptions { get; } = new[] { "debug", "info", "warn", "error" };

    /// <summary>モデル名の選択肢（Claude。その他は ComboBox で直接入力）。XAML バインディング用。</summary>
    public IReadOnlyList<string> ModelNameOptions { get; } = new[]
    {
        "claude-sonnet-4-20250514",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022",
        "claude-3-opus-20240229",
        "claude-3-haiku-20240307"
    };

    [ObservableProperty]
    private string _skillSavePath = string.Empty;

    [ObservableProperty]
    private string _skillLocalPath = string.Empty;

    [ObservableProperty]
    private string _screenshotPath = string.Empty;

    [ObservableProperty]
    private string _logLevel = "info";

    [ObservableProperty]
    private string _logPath = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _modelName = "claude-sonnet-4-20250514";

    [ObservableProperty]
    private string _apiEndpoint = string.Empty;

    [ObservableProperty]
    private string _repoRoot = string.Empty;

    /// <summary>ViewModel を生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    /// <param name="onClose">ウィンドウを閉じる際のコールバック。</param>
    public SettingsViewModel(ISettingsService settingsService, Action? onClose = null)
    {
        _settingsService = settingsService;
        _onClose = onClose;
        LoadFromService();
    }

    /// <summary>設定サービスから現在の設定を読み込む。</summary>
    private void LoadFromService()
    {
        var s = _settingsService.Get();
        SkillSavePath = s.SkillSavePath;
        SkillLocalPath = s.SkillLocalPath;
        ScreenshotPath = s.ScreenshotPath;
        LogLevel = s.LogLevel;
        LogPath = s.LogPath;
        ApiKey = s.ApiKey;
        ModelName = s.ModelName;
        ApiEndpoint = s.ApiEndpoint;
        RepoRoot = s.RepoRoot;
    }

    /// <summary>保存してウィンドウを閉じる。</summary>
    [RelayCommand]
    private void SaveAndClose()
    {
        _settingsService.Save(new AppSettings
        {
            SkillSavePath = SkillSavePath,
            SkillLocalPath = SkillLocalPath,
            ScreenshotPath = ScreenshotPath,
            LogLevel = LogLevel,
            LogPath = LogPath,
            ApiKey = ApiKey,
            ModelName = ModelName,
            ApiEndpoint = ApiEndpoint,
            RepoRoot = RepoRoot
        });
        _onClose?.Invoke();
    }

    /// <summary>保存せずにウィンドウを閉じる。</summary>
    [RelayCommand]
    private void Cancel()
    {
        _onClose?.Invoke();
    }
}

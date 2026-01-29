using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Shogun.Avalonia;

/// <summary>
/// 設定画面のコードビハインド。
/// パス系は参照ボタンでフォルダ選択ダイアログから選択させる。
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private async void OnBrowseSkillSavePath(object? sender, RoutedEventArgs e)
    {
        await PickFolderAndSet(p => (DataContext as ViewModels.SettingsViewModel)!.SkillSavePath = p, "スキル保存先を選択");
    }

    private async void OnBrowseSkillLocalPath(object? sender, RoutedEventArgs e)
    {
        await PickFolderAndSet(p => (DataContext as ViewModels.SettingsViewModel)!.SkillLocalPath = p, "ローカルスキル保存先を選択");
    }

    private async void OnBrowseScreenshotPath(object? sender, RoutedEventArgs e)
    {
        await PickFolderAndSet(p => (DataContext as ViewModels.SettingsViewModel)!.ScreenshotPath = p, "スクリーンショット保存先を選択");
    }

    private async void OnBrowseLogPath(object? sender, RoutedEventArgs e)
    {
        await PickFolderAndSet(p => (DataContext as ViewModels.SettingsViewModel)!.LogPath = p, "ログ保存先を選択");
    }

    /// <summary>フォルダ選択ダイアログを表示し、選択されたパスを指定アクションに渡す。</summary>
    private async System.Threading.Tasks.Task PickFolderAndSet(Action<string> setPath, string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            setPath(path);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace Shogun.Avalonia.Models;

/// <summary>
/// プロジェクトモデル。
/// </summary>
public partial class Project : ObservableObject
{
    /// <summary>プロジェクトID（一意）。</summary>
    [ObservableProperty]
    private string _id = string.Empty;

    /// <summary>プロジェクト名。</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>プロジェクトパス。</summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>優先度（high, medium, low 等）。</summary>
    [ObservableProperty]
    private string _priority = "medium";

    /// <summary>ステータス（active, inactive, archived 等）。</summary>
    [ObservableProperty]
    private string _status = "active";

    /// <summary>Notion URL（任意）。</summary>
    [ObservableProperty]
    private string _notionUrl = string.Empty;

    /// <summary>説明（任意）。</summary>
    [ObservableProperty]
    private string _description = string.Empty;
}

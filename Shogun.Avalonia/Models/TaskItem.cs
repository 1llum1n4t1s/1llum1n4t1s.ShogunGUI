using CommunityToolkit.Mvvm.ComponentModel;

namespace Shogun.Avalonia.Models;

/// <summary>
/// タスクアイテムモデル。
/// </summary>
public partial class TaskItem : ObservableObject
{
    /// <summary>タスクID。</summary>
    [ObservableProperty]
    private string _id = string.Empty;

    /// <summary>タスクの説明。</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>タスクのステータス（pending, in_progress, completed, cancelled）。</summary>
    [ObservableProperty]
    private string _status = "pending";

    /// <summary>タスクの優先度（high, medium, low）。</summary>
    [ObservableProperty]
    private string _priority = "medium";

    /// <summary>関連するプロジェクトID。</summary>
    [ObservableProperty]
    private string _projectId = string.Empty;
}

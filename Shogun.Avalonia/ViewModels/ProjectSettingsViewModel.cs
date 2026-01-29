using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shogun.Avalonia.Models;
using Shogun.Avalonia.Services;

namespace Shogun.Avalonia.ViewModels;

/// <summary>
/// プロジェクト設定画面の ViewModel。
/// </summary>
public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly Action? _onClose;

    [ObservableProperty]
    private ObservableCollection<Project> _projects = new();

    [ObservableProperty]
    private Project? _selectedProject;

    partial void OnSelectedProjectChanged(Project? value)
    {
        OnPropertyChanged(nameof(IsProjectSelected));
    }

    /// <summary>プロジェクトが選択されているか。</summary>
    public bool IsProjectSelected => SelectedProject != null;

    /// <summary>優先度の選択肢。</summary>
    public IReadOnlyList<string> PriorityOptions { get; } = new[] { "high", "medium", "low" };

    /// <summary>ステータスの選択肢。</summary>
    public IReadOnlyList<string> StatusOptions { get; } = new[] { "active", "inactive", "archived" };

    /// <summary>ViewModel を生成する。</summary>
    /// <param name="projectService">プロジェクトサービス。</param>
    /// <param name="onClose">ウィンドウを閉じる際のコールバック。</param>
    public ProjectSettingsViewModel(IProjectService projectService, Action? onClose = null)
    {
        _projectService = projectService;
        _onClose = onClose;
        LoadProjects();
    }

    /// <summary>プロジェクト一覧を読み込む。</summary>
    private void LoadProjects()
    {
        var projects = _projectService.GetProjects();
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }
    }

    /// <summary>新規プロジェクトを追加する。</summary>
    [RelayCommand]
    private void AddProject()
    {
        var newProject = new Project
        {
            Id = $"project_{DateTime.Now:yyyyMMddHHmmss}",
            Name = "新規プロジェクト",
            Path = "",
            Priority = "medium",
            Status = "active"
        };
        Projects.Add(newProject);
        SelectedProject = newProject;
    }

    /// <summary>選択中のプロジェクトを削除する。</summary>
    [RelayCommand]
    private void DeleteProject()
    {
        if (SelectedProject != null && Projects.Contains(SelectedProject))
        {
            Projects.Remove(SelectedProject);
            SelectedProject = Projects.FirstOrDefault();
        }
    }

    /// <summary>保存してウィンドウを閉じる。</summary>
    [RelayCommand]
    private void SaveAndClose()
    {
        _projectService.SaveProjects(Projects.ToList());
        _onClose?.Invoke();
    }

    /// <summary>保存せずにウィンドウを閉じる。</summary>
    [RelayCommand]
    private void Cancel()
    {
        _onClose?.Invoke();
    }
}

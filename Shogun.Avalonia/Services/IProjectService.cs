using System.Collections.Generic;
using Shogun.Avalonia.Models;

namespace Shogun.Avalonia.Services;

/// <summary>
/// プロジェクト設定の読み込み・保存を行うサービス。
/// </summary>
public interface IProjectService
{
    /// <summary>プロジェクト一覧を取得する。</summary>
    List<Project> GetProjects();

    /// <summary>プロジェクト一覧を保存する。</summary>
    void SaveProjects(List<Project> projects);
}

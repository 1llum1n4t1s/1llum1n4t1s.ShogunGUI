using System.Collections.Generic;

namespace Shogun.Avalonia.Models;

/// <summary>
/// プロジェクト一覧設定モデル。
/// </summary>
public class ProjectsConfig
{
    /// <summary>プロジェクト一覧。</summary>
    public List<Project> Projects { get; set; } = new();
}

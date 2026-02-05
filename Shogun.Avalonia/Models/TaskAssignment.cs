using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Shogun.Avalonia.Models;

/// <summary>
/// 家老が返すタスク割り当て（YAML パース用）。
/// </summary>
public partial class TaskAssignmentYaml
{
    /// <summary>割り当て一覧。</summary>
    [YamlMember(Alias = "tasks")]
    public List<TaskAssignmentItem>? Assignments { get; set; }
}

/// <summary>
/// 1 件の足軽へのタスク割り当て。
/// </summary>
public partial class TaskAssignmentItem
{
    /// <summary>足軽番号（1～8）。</summary>
    [YamlMember(Alias = "ashigaru_id")]
    public int Ashigaru { get; set; }

    /// <summary>タスク ID（例: cmd_001_1）。</summary>
    public string? TaskId { get; set; }

    /// <summary>親コマンド ID（例: cmd_001）。</summary>
    public string? ParentCmd { get; set; }

    /// <summary>タスク説明。</summary>
    public string? Description { get; set; }

    /// <summary>対象パス（任意）。</summary>
    public string? TargetPath { get; set; }
}

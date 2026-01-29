using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shogun.Avalonia.Models;

/// <summary>
/// 家老が返すタスク割り当て（JSON パース用）。
/// </summary>
public class TaskAssignmentJson
{
    /// <summary>割り当て一覧。</summary>
    [JsonPropertyName("assignments")]
    public List<TaskAssignmentItem>? Assignments { get; set; }
}

/// <summary>
/// 1 件の足軽へのタスク割り当て。
/// </summary>
public class TaskAssignmentItem
{
    /// <summary>足軽番号（1～8）。</summary>
    [JsonPropertyName("ashigaru")]
    public int Ashigaru { get; set; }

    /// <summary>タスク ID（例: cmd_001_1）。</summary>
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    /// <summary>親コマンド ID（例: cmd_001）。</summary>
    [JsonPropertyName("parent_cmd")]
    public string? ParentCmd { get; set; }

    /// <summary>タスク説明。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>対象パス（任意）。</summary>
    [JsonPropertyName("target_path")]
    public string? TargetPath { get; set; }
}

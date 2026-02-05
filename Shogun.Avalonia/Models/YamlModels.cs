using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Shogun.Avalonia.Models;

public partial class ProjectsWrapper
{
    [YamlMember(Alias = "projects")]
    public List<Project> Projects { get; set; } = new();
}

public partial class ShogunQueueWrapper
{
    [YamlMember(Alias = "queue")]
    public List<ShogunCommand> Queue { get; set; } = new();
}

public partial class TaskWrapper
{
    [YamlMember(Alias = "task")]
    public TaskItemYaml? Task { get; set; }
}

public partial class TaskItemYaml
{
    public string? TaskId { get; set; }
    public string? ParentCmd { get; set; }
    public string? Description { get; set; }
    public string? TargetPath { get; set; }
    public string? Status { get; set; }
    public string? Timestamp { get; set; }
}

public partial class ReportYaml
{
    public string? WorkerId { get; set; }
    public string? TaskId { get; set; }
    public string? Timestamp { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public SkillCandidateYaml? SkillCandidate { get; set; }
}

public partial class SkillCandidateYaml
{
    public bool Found { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Reason { get; set; }
}

public partial class MasterStatusYaml
{
    public string? LastUpdated { get; set; }
    public string? CurrentTask { get; set; }
    public string? TaskStatus { get; set; }
    public string? TaskDescription { get; set; }
    public Dictionary<string, AgentStatusYaml> Agents { get; set; } = new();
}

public partial class AgentStatusYaml
{
    public string? Status { get; set; }
    public string? LastAction { get; set; }
    public int? CurrentSubtasks { get; set; }
    public string? CurrentTask { get; set; }
    public int? Progress { get; set; }
    public string? LastCompleted { get; set; }
}

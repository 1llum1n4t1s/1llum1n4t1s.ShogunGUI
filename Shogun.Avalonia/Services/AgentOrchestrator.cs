using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Shogun.Avalonia.Models;
using Shogun.Avalonia.Util;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 将軍→家老→足軽のフローをアプリ内で完結させるオーケストレーター。
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IShogunQueueService _queueService;
    private readonly IAiService _aiService;
    private readonly IInstructionsLoader _instructionsLoader;
    private readonly ISettingsService _settingsService;

    /// <summary>サービスを生成する。</summary>
    public AgentOrchestrator(IShogunQueueService queueService, IAiService aiService, IInstructionsLoader instructionsLoader, ISettingsService? settingsService = null)
    {
        _queueService = queueService;
        _aiService = aiService;
        _instructionsLoader = instructionsLoader;
        _settingsService = settingsService ?? new SettingsService();
    }

    /// <inheritdoc />
    public async Task<string> ResolveShogunCommandAsync(string userInput, string? projectId, CancellationToken cancellationToken = default)
    {
        if (!_aiService.IsAvailable)
        {
            Logger.Log("ResolveShogunCommandAsync: AIサービスが利用不可のため入力をそのまま返します。", LogLevel.Debug);
            return userInput;
        }

        Logger.Log($"ResolveShogunCommandAsync 開始: userInput='{userInput}'", LogLevel.Info);
        var shogunInstructions = _instructionsLoader.LoadShogunInstructions() ?? "";
        var claudeMd = _instructionsLoader.LoadClaudeMd();
        var globalContext = _instructionsLoader.LoadGlobalContext();
        
        var systemPrefix = "";
        if (!string.IsNullOrWhiteSpace(claudeMd))
            systemPrefix += $"以下はシステム構成（CLAUDE.md）である。参照せよ。\n\n---\n{claudeMd}\n---\n\n";
        if (!string.IsNullOrWhiteSpace(globalContext))
            systemPrefix += $"以下はシステム全体の設定・殿の好み（memory/global_context.md）である。参照してから判断せよ。\n\n---\n{globalContext}\n---\n\n";

        var userMessage = $"殿の指示: {userInput}\nプロジェクト: {(string.IsNullOrEmpty(projectId) ? "未指定" : projectId)}";
        var settings = _settingsService.Get();
        var modelShogun = !string.IsNullOrWhiteSpace(settings.ModelShogun) ? settings.ModelShogun : null;
        
        Logger.Log($"将軍AIに問い合わせます。Model='{modelShogun ?? "Default"}'", LogLevel.Debug);
        var response = await _aiService.SendWithSystemAsync(systemPrefix + shogunInstructions, userMessage, modelShogun, cancellationToken);
        Logger.Log($"将軍AI応答: {response}", LogLevel.Debug);

        var trimmed = response.Trim();
        var codeBlock = Regex.Match(trimmed, @"```(?:[\w]*)\s*([\s\S]*?)```");
        if (codeBlock.Success)
            trimmed = codeBlock.Groups[1].Value.Trim();
        
        var result = string.IsNullOrWhiteSpace(trimmed) ? userInput : trimmed;
        Logger.Log($"ResolveShogunCommandAsync 完了: result='{result}'", LogLevel.Info);
        return result;
    }

    /// <inheritdoc />
    public async Task<string> RunAsync(string commandId, CancellationToken cancellationToken = default)
    {
        Logger.Log($"AgentOrchestrator.RunAsync 開始: commandId='{commandId}'", LogLevel.Info);
        var queue = _queueService.ReadShogunToKaro();
        var cmd = queue.FirstOrDefault(c => string.Equals(c.Id, commandId, StringComparison.Ordinal));
        if (cmd == null)
        {
            Logger.Log($"エラー: コマンド {commandId} が見つかりません。", LogLevel.Error);
            return $"エラー: コマンド {commandId} が見つかりません。";
        }

        if (!_aiService.IsAvailable)
        {
            Logger.Log("RunAsync: AIサービスが利用不可です。", LogLevel.Warning);
            return "エラー: 当アプリでは API 呼び出しは行いません。家老・足軽の実行は upstream の Claude Code CLI 等で行ってください。";
        }

        try
        {
            _queueService.UpdateCommandStatus(commandId, "in_progress");
            _queueService.WriteMasterStatus(DateTime.Now, commandId, "in_progress", cmd.Command, null);
            var projectsYaml = ReadProjectsYaml();
            var dashboardBefore = _queueService.ReadDashboardMd();
            var claudeMd = _instructionsLoader.LoadClaudeMd();
            var globalContext = _instructionsLoader.LoadGlobalContext();
            var karoInstructions = _instructionsLoader.LoadKaroInstructions() ?? "";

            var systemPrefix = "";
            if (!string.IsNullOrWhiteSpace(claudeMd))
                systemPrefix += $"以下はシステム構成（CLAUDE.md）である。参照せよ。\n\n---\n{claudeMd}\n---\n\n";
            if (!string.IsNullOrWhiteSpace(globalContext))
                systemPrefix += $"以下はシステム全体の設定・殿の好み（memory/global_context.md）である。参照してから判断せよ。\n\n---\n{globalContext}\n---\n\n";

            var ashigaruCount = _queueService.GetAshigaruCount();
            var karoUser = $@"将軍から以下の指示が届いた。分解して足軽に割り当てよ。

Command ID: {cmd.Id}
Command: {cmd.Command}
Project: {cmd.Project ?? "（未指定）"}
Priority: {cmd.Priority ?? "medium"}

{projectsYaml}

上記を踏まえ、1～{ashigaruCount}の足軽に任務を振り分けよ。
【重要】分割可能な作業は、可能な限り多くの足軽に分散して並列実行させよ。「1名で済む」と判断せず、並列化を最大化せよ。

出力は必ず以下のJSON形式のみ。他に説明やマークダウンは書くな。
{{\""assignments\"": [{{\""ashigaru\"": 1, \""task_id\"": \""{cmd.Id}_1\"", \""parent_cmd\"": \""{cmd.Id}\"", \""description\"": \""...\"", \""target_path\"": \""...\""}}, ...]}}";
            var settings = _settingsService.Get();
            var modelKaro = !string.IsNullOrWhiteSpace(settings.ModelKaro) ? settings.ModelKaro : null;
            
            Logger.Log($"家老AIにタスク分解を依頼します。Model='{modelKaro ?? "Default"}'", LogLevel.Debug);
            var karoResponse = await _aiService.SendWithSystemAsync(systemPrefix + karoInstructions, karoUser, modelKaro, cancellationToken);
            Logger.Log($"家老AI応答: {karoResponse}", LogLevel.Debug);

            var assignments = ParseTaskAssignments(karoResponse);
            if (assignments == null || assignments.Count == 0)
            {
                Logger.Log("家老がタスクを分解できませんでした。", LogLevel.Warning);
                _queueService.UpdateCommandStatus(commandId, "pending");
                return "家老がタスクを分解できませんでした。応答を確認してください。";
            }

            Logger.Log($"{assignments.Count} 件のタスクが割り当てられました。", LogLevel.Info);
            var timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            foreach (var a in assignments)
            {
                if (a.Ashigaru < 1 || a.Ashigaru > ashigaruCount)
                    continue;
                _queueService.WriteTaskYaml(a.Ashigaru, a.TaskId ?? $"{cmd.Id}_{a.Ashigaru}", a.ParentCmd ?? cmd.Id, a.Description ?? "", a.TargetPath, "in_progress", timestamp);
            }
            UpdateDashboardInProgress(dashboardBefore, cmd.Id, assignments);
            
            var ashigaruInstructions = _instructionsLoader.LoadAshigaruInstructions() ?? "";
            var modelAshigaru = !string.IsNullOrWhiteSpace(settings.ModelAshigaru) ? settings.ModelAshigaru : null;
            
            Logger.Log("足軽の並列実行を開始します。", LogLevel.Info);
            var reportTasks = assignments.Where(a => a.Ashigaru >= 1 && a.Ashigaru <= ashigaruCount).Select(async a =>
            {
                var taskContent = _queueService.ReadTaskYaml(a.Ashigaru);
                var ashigaruUser = systemPrefix + $@"以下の任務を実行し、結果を報告せよ。スキル化候補の有無は毎回必ず記入せよ。

{taskContent}

出力は必ず以下のJSON形式のみ。他に説明やマークダウンは書くな。
{{\""task_id\"": \""{a.TaskId}\"", \""status\"": \""done\"", \""result\"": \""（実行結果の要約）\"", \""skill_candidate_found\"": false, \""skill_candidate_name\"": null, \""skill_candidate_description\"": null, \""skill_candidate_reason\"": null}}
※ skill_candidate_found が true のときは name, description, reason を記入せよ。";
                
                Logger.Log($"足軽{a.Ashigaru} AIに任務を依頼します。Model='{modelAshigaru ?? "Default"}'", LogLevel.Debug);
                var response = await _aiService.SendWithSystemAsync(ashigaruInstructions, ashigaruUser, modelAshigaru, cancellationToken);
                Logger.Log($"足軽{a.Ashigaru} AI応答: {response}", LogLevel.Debug);

                var report = ParseAshigaruReport(response);
                var ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                _queueService.WriteReportYaml(a.Ashigaru, a.TaskId ?? "", ts, report?.Status ?? "done", report?.Result ?? response, report?.SkillCandidateFound ?? false, report?.SkillCandidateName, report?.SkillCandidateDescription, report?.SkillCandidateReason);
                return (report, a);
            });
            var reportResults = await Task.WhenAll(reportTasks);
            Logger.Log("全足軽の実行が完了しました。", LogLevel.Info);
            var dashboardAfter = _queueService.ReadDashboardMd();
            var resultRows = reportResults.Select(r =>
            {
                var reportContent = _queueService.ReadReportYaml(r.a.Ashigaru);
                var result = ExtractReportResult(reportContent);
                return (DateTime.Now.ToString("HH:mm"), cmd.Project ?? "-", r.a.Description ?? "", result);
            }).ToList();
            UpdateDashboardResults(dashboardAfter, resultRows);
            var skillCandidates = reportResults.Where(r => r.report?.SkillCandidateFound == true && !string.IsNullOrWhiteSpace(r.report.SkillCandidateName)).Select(r => r.report!).ToList();
            if (skillCandidates.Count > 0)
            {
                var dashboardWithSkills = _queueService.ReadDashboardMd();
                foreach (var sk in skillCandidates)
                {
                    var line = $"- **{sk.SkillCandidateName}**: {sk.SkillCandidateDescription ?? ""}（理由: {sk.SkillCandidateReason ?? ""}）";
                    dashboardWithSkills = AppendToDashboardSection(dashboardWithSkills, "スキル化候補", line);
                    
                    // 🚨 要対応セクションへの記載（サマリ）
                    var summaryLine = $"- スキル化候補: **{sk.SkillCandidateName}** 【承認待ち】";
                    dashboardWithSkills = AppendToDashboardSection(dashboardWithSkills, "要対応", summaryLine);
                }
                _queueService.WriteDashboardMd(dashboardWithSkills);
            }
            _queueService.UpdateCommandStatus(commandId, "done");
            _queueService.WriteMasterStatus(DateTime.Now, commandId, "done", cmd.Command, assignments);
            return $"指示 {commandId} を完了しました。家老がタスクを分配し、足軽が実行・報告し、dashboard を更新しました。";
        }
        catch (Exception ex)
        {
            _queueService.UpdateCommandStatus(commandId, "pending");
            _queueService.WriteMasterStatus(DateTime.Now, commandId, "failed", null, null);
            return $"エラー: {ex.Message}";
        }
    }

    private static string ExtractJsonFromResponse(string response)
    {
        var trimmed = response.Trim();
        var match = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
            return trimmed.Substring(start, end - start + 1);
        return trimmed;
    }

    private static List<TaskAssignmentItem>? ParseTaskAssignments(string response)
    {
        try
        {
            var json = ExtractJsonFromResponse(response);
            var obj = JsonSerializer.Deserialize<TaskAssignmentJson>(json);
            return obj?.Assignments;
        }
        catch
        {
            return null;
        }
    }

    private static AshigaruReportJson? ParseAshigaruReport(string response)
    {
        try
        {
            var json = ExtractJsonFromResponse(response);
            return JsonSerializer.Deserialize<AshigaruReportJson>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractReportResult(string reportYaml)
    {
        if (string.IsNullOrWhiteSpace(reportYaml))
            return "-";
        var line = reportYaml.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("result:", StringComparison.Ordinal));
        if (line == null)
            return "-";
        var value = line.IndexOf(':', StringComparison.Ordinal) >= 0 ? line.Substring(line.IndexOf(':', StringComparison.Ordinal) + 1).Trim() : "";
        if (value.StartsWith('"') && value.EndsWith('"'))
            value = value.Substring(1, value.Length - 2).Replace("\\\"", "\"");
        return string.IsNullOrEmpty(value) ? "-" : value;
    }

    private string ReadProjectsYaml()
    {
        var path = System.IO.Path.Combine(_queueService.GetRepoRoot(), "config", "projects.yaml");
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
    }

    private void UpdateDashboardInProgress(string currentDashboard, string commandId, List<TaskAssignmentItem> assignments)
    {
        var inProgressContent = $"- {commandId}: タスク分配済み（" + string.Join(", ", assignments.Select(a => $"足軽{a.Ashigaru}: {(a.Description?.Length > 20 ? a.Description.Substring(0, 20) + "…" : a.Description)}")) + "）";
        var newContent = AppendToDashboardSection(currentDashboard, "進行中", inProgressContent);
        var updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        newContent = Regex.Replace(newContent, @"最終更新[^\n]*", "最終更新: " + updated);
        _queueService.WriteDashboardMd(newContent);
    }

    private void UpdateDashboardResults(string currentDashboard, List<(string time, string battlefield, string mission, string result)> rows)
    {
        if (rows.Count == 0)
            return;
        var lines = currentDashboard.Split('\n').ToList();
        var updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var tableRows = rows.Select(r => $"| {r.time} | {EscapeTable(r.battlefield)} | {EscapeTable(r.mission)} | {EscapeTable(r.result)} |").ToList();
        var newContent = AppendDashboardTableRows(lines, "本日の戦果", tableRows, updated);
        _queueService.WriteDashboardMd(newContent);
    }

    private static string ReplaceDashboardSection(List<string> lines, string sectionKeyword, string newBody, string updatedTimestamp)
    {
        var result = new List<string>();
        var i = 0;
        while (i < lines.Count)
        {
            result.Add(lines[i]);
            if (lines[i].Contains("最終更新", StringComparison.Ordinal))
            {
                i++;
                result[result.Count - 1] = "最終更新: " + updatedTimestamp;
                if (lines[i].Contains("(Last Updated)", StringComparison.Ordinal))
                    result[result.Count - 1] += " (Last Updated)";
                continue;
            }
            if (lines[i].StartsWith("## ", StringComparison.Ordinal) && lines[i].Contains(sectionKeyword, StringComparison.Ordinal))
            {
                i++;
                result.Add(newBody);
                while (i < lines.Count && !lines[i].TrimStart().StartsWith("## ", StringComparison.Ordinal))
                    i++;
                continue;
            }
            i++;
        }
        return string.Join(Environment.NewLine, result);
    }

    private static string AppendDashboardTableRows(List<string> lines, string sectionKeyword, List<string> newRows, string updatedTimestamp)
    {
        var result = new List<string>();
        var i = 0;
        while (i < lines.Count)
        {
            result.Add(lines[i]);
            if (lines[i].Contains("最終更新", StringComparison.Ordinal))
            {
                var idx = result.Count - 1;
                result[idx] = Regex.Replace(result[idx], @"最終更新[^\n]*", "最終更新: " + updatedTimestamp);
                i++;
                continue;
            }
            if (lines[i].StartsWith("## ", StringComparison.Ordinal) && lines[i].Contains(sectionKeyword, StringComparison.Ordinal))
            {
                i++;
                while (i < lines.Count && !lines[i].TrimStart().StartsWith("## ", StringComparison.Ordinal))
                {
                    result.Add(lines[i]);
                    if (lines[i].StartsWith("|", StringComparison.Ordinal) && lines[i].Contains("---", StringComparison.Ordinal))
                    {
                        foreach (var row in newRows)
                            result.Add(row);
                    }
                    i++;
                }
                continue;
            }
            i++;
        }
        return string.Join(Environment.NewLine, result);
    }

    private static string EscapeTable(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "-";
        return s.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", "").Replace("\n", " ");
    }

    /// <summary>指定セクションの末尾に 1 行を追記する（フォーク元: 進行中・スキル化候補・要対応の追記用）。</summary>
    private static string AppendToDashboardSection(string currentDashboard, string sectionKeyword, string newLine)
    {
        var lines = currentDashboard.Split('\n').ToList();
        var result = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            result.Add(lines[i]);
            if (lines[i].StartsWith("## ", StringComparison.Ordinal) && lines[i].Contains(sectionKeyword, StringComparison.Ordinal))
            {
                i++;
                while (i < lines.Count && !lines[i].TrimStart().StartsWith("## ", StringComparison.Ordinal))
                {
                    result.Add(lines[i]);
                    i++;
                }
                result.Add(newLine);
                if (i < lines.Count)
                    result.Add(lines[i]);
            }
        }
        return string.Join(Environment.NewLine, result);
    }
}

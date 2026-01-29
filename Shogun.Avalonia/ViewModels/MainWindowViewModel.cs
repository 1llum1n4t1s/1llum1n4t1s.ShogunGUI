using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaEdit.Document;
using Shogun.Avalonia;
using Shogun.Avalonia.Models;
using Shogun.Avalonia.Services;

namespace Shogun.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private IAiService _aiService;
    private readonly IShogunQueueService _queueService;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ISettingsService _settingsService;

    /// <summary>各エージェントのペイン（将軍・家老・足軽1～8）。多カラム表示用。</summary>
    public ObservableCollection<AgentPaneViewModel> AgentPanes { get; } = new();

    /// <summary>左ペイン用（将軍・家老）。</summary>
    public AgentPaneViewModel? LeftPane0 => AgentPanes.Count > 0 ? AgentPanes[0] : null;
    public AgentPaneViewModel? LeftPane1 => AgentPanes.Count > 1 ? AgentPanes[1] : null;
    /// <summary>中央ペイン用（足軽1～4）。</summary>
    public AgentPaneViewModel? CenterPane0 => AgentPanes.Count > 2 ? AgentPanes[2] : null;
    public AgentPaneViewModel? CenterPane1 => AgentPanes.Count > 3 ? AgentPanes[3] : null;
    public AgentPaneViewModel? CenterPane2 => AgentPanes.Count > 4 ? AgentPanes[4] : null;
    public AgentPaneViewModel? CenterPane3 => AgentPanes.Count > 5 ? AgentPanes[5] : null;
    /// <summary>右ペイン用（足軽5～8）。</summary>
    public AgentPaneViewModel? RightPane0 => AgentPanes.Count > 6 ? AgentPanes[6] : null;
    public AgentPaneViewModel? RightPane1 => AgentPanes.Count > 7 ? AgentPanes[7] : null;
    public AgentPaneViewModel? RightPane2 => AgentPanes.Count > 8 ? AgentPanes[8] : null;
    public AgentPaneViewModel? RightPane3 => AgentPanes.Count > 9 ? AgentPanes[9] : null;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _chatMessages = new();

    [ObservableProperty]
    private ObservableCollection<TaskItem> _allTasks = new();

    /// <summary>表示用のタスク一覧（選択中のプロジェクトのタスクのみ）。</summary>
    public ObservableCollection<TaskItem> Tasks
    {
        get
        {
            var filtered = new ObservableCollection<TaskItem>();
            var projectId = SelectedProject?.Id ?? "";
            foreach (var task in AllTasks.Where(t => t.ProjectId == projectId || string.IsNullOrEmpty(projectId)))
            {
                filtered.Add(task);
            }
            return filtered;
        }
    }

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private TextDocument? _codeDocument;

    [ObservableProperty]
    private ObservableCollection<Project> _projects = new();

    [ObservableProperty]
    private Project? _selectedProject;

    partial void OnSelectedProjectChanged(Project? value)
    {
        OnPropertyChanged(nameof(Tasks));
    }

    [ObservableProperty]
    private bool _isAiProcessing = false;

    [ObservableProperty]
    private string _dashboardContent = string.Empty;

    public MainWindowViewModel(IProjectService? projectService = null, IAiService? aiService = null, IShogunQueueService? queueService = null, IAgentOrchestrator? orchestrator = null, ISettingsService? settingsService = null)
    {
        _projectService = projectService ?? new ProjectService();
        _settingsService = settingsService ?? new SettingsService();
        _aiService = aiService ?? new AiService(_settingsService);
        _queueService = queueService ?? new ShogunQueueService(_settingsService);
        _orchestrator = orchestrator ?? new AgentOrchestrator(_queueService, _aiService, new InstructionsLoader(_queueService));
        LoadProjects();
        InitializeDummyData();
        RefreshDashboard();
    }

    /// <summary>dashboard.md を読み込み表示を更新する。</summary>
    [RelayCommand]
    private void RefreshDashboard()
    {
        DashboardContent = _queueService.ReadDashboardMd();
        if (string.IsNullOrEmpty(DashboardContent))
            DashboardContent = "（dashboard.md がありません。設定でワークスペースルートを指定してください）";
        RefreshAgentPanesFromQueue();
    }

    /// <summary>queue/tasks と queue/reports から各ペインの表示を更新する。</summary>
    private void RefreshAgentPanesFromQueue()
    {
        if (AgentPanes.Count < 2)
            return;
        var karoContent = "queue/shogun_to_karo.yaml の最新: " + string.Join("; ", _queueService.ReadShogunToKaro().Take(3).Select(c => c.Id + " " + c.Command));
        AgentPanes[1].Blocks.Clear();
        AgentPanes[1].Blocks.Add(new PaneBlock { Content = karoContent, Timestamp = DateTime.Now });
        for (var i = 1; i <= 8 && i + 1 < AgentPanes.Count; i++)
        {
            var task = _queueService.ReadTaskYaml(i);
            var report = _queueService.ReadReportYaml(i);
            AgentPanes[i + 1].Blocks.Clear();
            if (!string.IsNullOrEmpty(task))
                AgentPanes[i + 1].Blocks.Add(new PaneBlock { Content = "任務: " + task.Trim().Replace("\r\n", " ").Replace("\n", " "), Timestamp = DateTime.Now });
            if (!string.IsNullOrEmpty(report))
                AgentPanes[i + 1].Blocks.Add(new PaneBlock { Content = "報告: " + report.Trim().Replace("\r\n", " ").Replace("\n", " "), Timestamp = DateTime.Now });
        }
    }

    /// <summary>AIサービスを再初期化する（設定変更後）。</summary>
    public void RefreshAiService()
    {
        _aiService = new AiService(_settingsService);
        OnPropertyChanged(nameof(IsAiAvailable));
    }

    /// <summary>AIサービスが利用可能か。</summary>
    public bool IsAiAvailable => _aiService.IsAvailable;

    /// <summary>プロジェクト一覧を読み込む。</summary>
    public void LoadProjects()
    {
        var projects = _projectService.GetProjects();
        var currentSelectedId = SelectedProject?.Id;
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }
        SelectedProject = Projects.FirstOrDefault(p => p.Id == currentSelectedId) ?? Projects.FirstOrDefault();
        OnPropertyChanged(nameof(Tasks));
    }

    private void InitializeDummyData()
    {
        var paneNames = new List<string> { "将軍", "家老" };
        for (var i = 1; i <= AppConstants.AshigaruCount; i++)
            paneNames.Add($"足軽{i}");
        foreach (var name in paneNames)
        {
            var pane = new AgentPaneViewModel { DisplayName = name };
            pane.Blocks.Add(new PaneBlock
            {
                Content = name == "将軍"
                    ? "Shogun.Avalonia にようこそ。下の入力欄から指示を送れ。"
                    : "次の指示をお待ち申し上げる。",
                Timestamp = DateTime.Now
            });
            if (name == "将軍" && IsAiAvailable)
            {
                pane.Blocks.Add(new PaneBlock
                {
                    Content = "何かお手伝いできることはありますか？",
                    Timestamp = DateTime.Now
                });
            }
            else if (name == "将軍" && !IsAiAvailable)
            {
                pane.Blocks.Add(new PaneBlock
                {
                    Content = "AI機能を使用するには、設定画面でAPIキーとモデル名を設定してください。",
                    Timestamp = DateTime.Now
                });
            }
            AgentPanes.Add(pane);
        }

        OnPropertyChanged(nameof(LeftPane0));
        OnPropertyChanged(nameof(LeftPane1));
        OnPropertyChanged(nameof(CenterPane0));
        OnPropertyChanged(nameof(CenterPane1));
        OnPropertyChanged(nameof(CenterPane2));
        OnPropertyChanged(nameof(CenterPane3));
        OnPropertyChanged(nameof(RightPane0));
        OnPropertyChanged(nameof(RightPane1));
        OnPropertyChanged(nameof(RightPane2));
        OnPropertyChanged(nameof(RightPane3));

        var welcomeMessage = new ChatMessage
        {
            Sender = "system",
            Content = "Shogun.Avalonia にようこそ",
            ProjectId = ""
        };
        ChatMessages.Add(welcomeMessage);

        if (IsAiAvailable)
        {
            var aiMessage = new ChatMessage
            {
                Sender = "ai",
                Content = "何かお手伝いできることはありますか？",
                ProjectId = ""
            };
            ChatMessages.Add(aiMessage);
        }
        else
        {
            var aiMessage = new ChatMessage
            {
                Sender = "system",
                Content = "AI機能を使用するには、設定画面でAPIキーとモデル名を設定してください。",
                ProjectId = ""
            };
            ChatMessages.Add(aiMessage);
        }

        CodeDocument = new TextDocument(string.Empty);
    }

    /// <summary>メッセージを送信する（将軍→家老: queue 書き込み + アプリ内オーケストレーターで家老・足軽実行）。</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || IsAiProcessing)
            return;

        var projectId = SelectedProject?.Id ?? "";
        var inputCopy = ChatInput;
        ChatInput = string.Empty;
        IsAiProcessing = true;

        var userMessage = new ChatMessage
        {
            Sender = "user",
            Content = inputCopy,
            ProjectId = projectId,
            Timestamp = DateTime.Now
        };
        ChatMessages.Add(userMessage);

        if (AgentPanes.Count > 0)
            AgentPanes[0].Blocks.Add(new PaneBlock { Content = inputCopy, Timestamp = DateTime.Now });

        try
        {
            var id = _queueService.AppendCommand(inputCopy, string.IsNullOrEmpty(projectId) ? null : projectId, "medium");
            string resultMessage;
            if (_aiService.IsAvailable)
            {
                resultMessage = $"指示をキューに追加しました（{id}）。家老・足軽を実行中…";
                var progressMessage = new ChatMessage
                {
                    Sender = "system",
                    Content = resultMessage,
                    ProjectId = projectId,
                    Timestamp = DateTime.Now
                };
                ChatMessages.Add(progressMessage);
                if (AgentPanes.Count > 0)
                    AgentPanes[0].Blocks.Add(new PaneBlock { Content = resultMessage, Timestamp = DateTime.Now });
                var runResult = await _orchestrator.RunAsync(id);
                resultMessage = runResult;
            }
            else
            {
                resultMessage = $"指示をキューに追加しました（{id}）。APIキー未設定のため家老・足軽は実行されません。ダッシュボードで確認してください。";
            }
            var sysMessage = new ChatMessage
            {
                Sender = "system",
                Content = resultMessage,
                ProjectId = projectId,
                Timestamp = DateTime.Now
            };
            ChatMessages.Add(sysMessage);
            if (AgentPanes.Count > 0)
                AgentPanes[0].Blocks.Add(new PaneBlock { Content = resultMessage, Timestamp = DateTime.Now });
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            var errorMessage = new ChatMessage
            {
                Sender = "system",
                Content = $"エラー: {ex.Message}",
                ProjectId = projectId,
                Timestamp = DateTime.Now
            };
            ChatMessages.Add(errorMessage);
            if (AgentPanes.Count > 0)
                AgentPanes[0].Blocks.Add(new PaneBlock { Content = $"エラー: {ex.Message}", Timestamp = DateTime.Now });
        }
        finally
        {
            IsAiProcessing = false;
        }

        await Task.CompletedTask;
    }

    /// <summary>AI応答を処理してタスクやコードを抽出する。</summary>
    private async Task ProcessAiResponseAsync(string aiResponse, string projectId)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return;

        var lines = aiResponse.Split('\n', StringSplitOptions.None);
        var codeBlocks = new List<string>();
        var inCodeBlock = false;
        var currentCodeBlock = new List<string>();
        var codeBlockLanguage = "";

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    var code = string.Join("\n", currentCodeBlock);
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        codeBlocks.Add(code);
                    }
                    currentCodeBlock.Clear();
                    codeBlockLanguage = "";
                }
                else
                {
                    codeBlockLanguage = trimmed.Substring(3).Trim();
                }
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                currentCodeBlock.Add(line);
            }
            else
            {
                var taskDesc = ExtractTaskDescription(line);
                if (!string.IsNullOrWhiteSpace(taskDesc) && taskDesc.Length > 3)
                {
                    var existingTask = AllTasks.FirstOrDefault(t => 
                        t.Description.Equals(taskDesc, StringComparison.OrdinalIgnoreCase) && 
                        t.ProjectId == projectId);
                    if (existingTask == null)
                    {
                        var newTask = new TaskItem
                        {
                            Id = $"task_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}",
                            Description = taskDesc,
                            Status = "pending",
                            Priority = DeterminePriority(taskDesc),
                            ProjectId = projectId
                        };
                        AllTasks.Add(newTask);
                        OnPropertyChanged(nameof(Tasks));
                    }
                }
            }
        }

        if (codeBlocks.Count > 0)
        {
            var latestCode = codeBlocks.Last();
            CodeDocument = new TextDocument(latestCode);
        }
    }

    /// <summary>行からタスク説明を抽出する。</summary>
    private static string ExtractTaskDescription(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("• "))
        {
            return trimmed.Substring(2).Trim();
        }
        if (trimmed.StartsWith("-") || trimmed.StartsWith("*") || trimmed.StartsWith("•"))
        {
            return trimmed.Substring(1).Trim();
        }
        if (trimmed.StartsWith("1. ") || trimmed.StartsWith("2. ") || trimmed.StartsWith("3. ") ||
            trimmed.StartsWith("4. ") || trimmed.StartsWith("5. ") || trimmed.StartsWith("6. ") ||
            trimmed.StartsWith("7. ") || trimmed.StartsWith("8. ") || trimmed.StartsWith("9. "))
        {
            var dotIndex = trimmed.IndexOf(". ", StringComparison.Ordinal);
            if (dotIndex > 0)
            {
                return trimmed.Substring(dotIndex + 2).Trim();
            }
        }
        return string.Empty;
    }

    /// <summary>タスク説明から優先度を判定する。</summary>
    private static string DeterminePriority(string description)
    {
        var lower = description.ToLowerInvariant();
        if (lower.Contains("重要") || lower.Contains("緊急") || lower.Contains("urgent") || lower.Contains("critical"))
            return "high";
        if (lower.Contains("低") || lower.Contains("low") || lower.Contains("optional"))
            return "low";
        return "medium";
    }
}

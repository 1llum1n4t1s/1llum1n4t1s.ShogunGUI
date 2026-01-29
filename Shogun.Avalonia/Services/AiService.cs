using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Shogun.Avalonia.Models;

namespace Shogun.Avalonia.Services;

/// <summary>
/// Claude API（Anthropic）を使った AI チャットサービス。
/// </summary>
public class AiService : IAiService
{
    private readonly ISettingsService _settingsService;
    private AnthropicClient? _client;
    private string? _lastApiKey;
    private string? _lastModelName;

    /// <summary>サービスを生成する。</summary>
    /// <param name="settingsService">設定サービス。</param>
    public AiService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        InitializeClient();
    }

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            var settings = _settingsService.Get();
            return !string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ModelName);
        }
    }

    /// <inheritdoc />
    public async Task<string> SendChatMessageAsync(string message, string? projectContext = null, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return "エラー: APIキーまたはモデル名が設定されていません。設定画面で設定してください。";
        }

        try
        {
            InitializeClient();
            if (_client == null)
            {
                return "エラー: AIサービスを初期化できませんでした。";
            }

            var messages = new List<Message>
            {
                new(RoleType.User, message)
            };
            var systemMessages = string.IsNullOrEmpty(projectContext)
                ? null
                : new List<SystemMessage> { new($"現在のプロジェクト: {projectContext}") };
            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 4096,
                Model = _settingsService.Get().ModelName,
                Stream = false,
                Temperature = 0.7m,
                System = systemMessages
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
            return response.Message?.ToString() ?? "（応答が空でした）";
        }
        catch (Exception ex)
        {
            return $"エラー: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task<string> SendWithSystemAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return "エラー: APIキーまたはモデル名が設定されていません。";
        try
        {
            InitializeClient();
            if (_client == null)
                return "エラー: AIサービスを初期化できませんでした。";
            var messages = new List<Message> { new(RoleType.User, userMessage) };
            var systemMessages = string.IsNullOrEmpty(systemPrompt) ? null : new List<SystemMessage> { new(systemPrompt) };
            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 8192,
                Model = _settingsService.Get().ModelName,
                Stream = false,
                Temperature = 0.3m,
                System = systemMessages
            };
            var response = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
            return response.Message?.ToString() ?? "（応答が空でした）";
        }
        catch (Exception ex)
        {
            return $"エラー: {ex.Message}";
        }
    }

    /// <summary>Claude API クライアントを初期化または再初期化する。</summary>
    private void InitializeClient()
    {
        var settings = _settingsService.Get();

        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.ModelName))
        {
            _client = null;
            _lastApiKey = null;
            _lastModelName = null;
            return;
        }

        if (_lastApiKey == settings.ApiKey && _lastModelName == settings.ModelName && _client != null)
        {
            return;
        }

        _lastApiKey = settings.ApiKey;
        _lastModelName = settings.ModelName;
        _client = new AnthropicClient(new APIAuthentication(settings.ApiKey));
    }
}

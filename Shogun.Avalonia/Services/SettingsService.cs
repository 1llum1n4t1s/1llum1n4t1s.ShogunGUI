using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Shogun.Avalonia.Models;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 設定サービス。パス等は config/settings.yaml、API キー等は AppData の JSON で永続化する。
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _yamlPath;
    private readonly string _jsonPath;
    private AppSettings? _current;

    /// <summary>設定ファイルのパスを指定してインスタンスを生成する。</summary>
    /// <param name="yamlPath">config/settings.yaml のパス。null のとき既定の config ディレクトリを使用。</param>
    /// <param name="jsonPath">API キー等を保存する JSON のパス。null のとき AppData を使用。</param>
    public SettingsService(string? yamlPath = null, string? jsonPath = null)
    {
        _yamlPath = yamlPath ?? GetDefaultSettingsYamlPath();
        _jsonPath = jsonPath ?? GetDefaultSettingsJsonPath();
    }

    /// <inheritdoc />
    public AppSettings Get()
    {
        if (_current != null)
            return _current;

        EnsureYamlDefaults();
        var yaml = ReadYamlPaths();
        var api = ReadJsonApi();
        _current = new AppSettings
        {
            SkillSavePath = yaml.SkillSavePath,
            SkillLocalPath = yaml.SkillLocalPath,
            ScreenshotPath = yaml.ScreenshotPath,
            LogLevel = yaml.LogLevel,
            LogPath = yaml.LogPath,
            ApiKey = api.apiKey,
            ModelName = api.modelName,
            ApiEndpoint = api.apiEndpoint,
            RepoRoot = api.repoRoot
        };
        return _current;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        _current = settings;
        WriteYamlPaths(settings.SkillSavePath, settings.SkillLocalPath, settings.ScreenshotPath, settings.LogLevel, settings.LogPath);
        WriteJsonApi(settings.ApiKey, settings.ModelName, settings.ApiEndpoint, settings.RepoRoot);
    }

    /// <summary>config ディレクトリの既定パス（プロジェクトルートの config）。</summary>
    public static string GetDefaultConfigDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "config"));
        return configDir;
    }

    private static string GetDefaultSettingsYamlPath()
    {
        return Path.Combine(GetDefaultConfigDirectory(), "settings.yaml");
    }

    private static string GetDefaultSettingsJsonPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "Shogun.Avalonia");
        return Path.Combine(folder, "settings.json");
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>空の設定を補うときの初期パス（Windows: %USERPROFILE%、macOS: ~ のリテラルで YAML に保存）。</summary>
    private static (string SkillSavePath, string SkillLocalPath, string ScreenshotPath, string LogPath) GetDefaultPaths()
    {
        if (IsWindows)
        {
            return (
                "%USERPROFILE%\\.claude\\skills\\shogun",
                "%USERPROFILE%\\Documents\\shogun\\skills",
                "%USERPROFILE%\\Pictures\\shogun",
                "%USERPROFILE%\\Documents\\shogun\\logs"
            );
        }
        if (IsMacOs)
        {
            return (
                "~/.claude/skills/shogun",
                "~/Documents/shogun/skills",
                "~/Pictures/shogun",
                "~/Documents/shogun/logs"
            );
        }
        var fallbackHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(fallbackHome))
            fallbackHome = "~";
        return (
            $"{fallbackHome}/.claude/skills/shogun",
            $"{fallbackHome}/Documents/shogun/skills",
            $"{fallbackHome}/Pictures/shogun",
            $"{fallbackHome}/Documents/shogun/logs"
        );
    }

    private void EnsureYamlDefaults()
    {
        var dir = Path.GetDirectoryName(_yamlPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (!File.Exists(_yamlPath))
        {
            if (IsWindows)
            {
                WriteFullDefaultYaml(
                    "%USERPROFILE%\\.claude\\skills\\shogun",
                    "%USERPROFILE%\\Documents\\shogun\\skills",
                    "%USERPROFILE%\\Pictures\\shogun",
                    "%USERPROFILE%\\Documents\\shogun\\logs"
                );
            }
            else
            {
                WriteFullDefaultYaml(
                    "~/.claude/skills/shogun",
                    "~/Documents/shogun/skills",
                    "~/Pictures/shogun",
                    "~/Documents/shogun/logs"
                );
            }
            return;
        }

        var yaml = ReadYamlPaths();
        var needSave = string.IsNullOrWhiteSpace(yaml.SkillSavePath) || string.IsNullOrWhiteSpace(yaml.SkillLocalPath)
                      || string.IsNullOrWhiteSpace(yaml.ScreenshotPath) || string.IsNullOrWhiteSpace(yaml.LogPath);
        if (!needSave)
            return;

        var defaults = GetDefaultPaths();
        var newSkillSave = string.IsNullOrWhiteSpace(yaml.SkillSavePath) ? defaults.SkillSavePath : yaml.SkillSavePath;
        var newSkillLocal = string.IsNullOrWhiteSpace(yaml.SkillLocalPath) ? defaults.SkillLocalPath : yaml.SkillLocalPath;
        var newScreenshot = string.IsNullOrWhiteSpace(yaml.ScreenshotPath) ? defaults.ScreenshotPath : yaml.ScreenshotPath;
        var newLogPath = string.IsNullOrWhiteSpace(yaml.LogPath) ? defaults.LogPath : yaml.LogPath;
        WriteYamlPaths(newSkillSave, newSkillLocal, newScreenshot, yaml.LogLevel, newLogPath);
    }

    private void WriteFullDefaultYaml(string skillSave, string skillLocal, string screenshot, string logPath)
    {
        var quote = (string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        var content = $@"# multi-agent-shogun 設定ファイル

# 言語設定
language: ja

# スキル設定
skill:
  save_path: {quote(skillSave)}
  local_path: {quote(skillLocal)}

# スクリーンショット設定
screenshot:
  windows_path: {quote(screenshot)}
  path: """"

# ログ設定
logging:
  level: info
  path: {quote(logPath)}
";
        File.WriteAllText(_yamlPath, content);
    }

    private (string SkillSavePath, string SkillLocalPath, string ScreenshotPath, string LogLevel, string LogPath) ReadYamlPaths()
    {
        var skillSave = string.Empty;
        var skillLocal = string.Empty;
        var screenshotPath = string.Empty;
        var logLevel = "info";
        var logPath = string.Empty;

        if (!File.Exists(_yamlPath))
            return (skillSave, skillLocal, screenshotPath, logLevel, logPath);

        var lines = File.ReadAllLines(_yamlPath);
        var inSkill = false;
        var inScreenshot = false;
        var inLogging = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                inSkill = inScreenshot = inLogging = false;
                continue;
            }

            if (trimmed.StartsWith("skill:"))
            {
                inSkill = true;
                inScreenshot = inLogging = false;
                continue;
            }
            if (trimmed.StartsWith("screenshot:"))
            {
                inScreenshot = true;
                inSkill = inLogging = false;
                continue;
            }
            if (trimmed.StartsWith("logging:"))
            {
                inLogging = true;
                inSkill = inScreenshot = false;
                continue;
            }

            if (inSkill && trimmed.StartsWith("save_path:"))
            {
                skillSave = ExtractYamlValue(trimmed.Substring("save_path:".Length));
                continue;
            }
            if (inSkill && trimmed.StartsWith("local_path:"))
            {
                skillLocal = ExtractYamlValue(trimmed.Substring("local_path:".Length));
                continue;
            }
            if (inScreenshot && (trimmed.StartsWith("windows_path:") || trimmed.StartsWith("path:")))
            {
                if (trimmed.StartsWith("windows_path:"))
                    screenshotPath = ExtractYamlValue(trimmed.Substring("windows_path:".Length));
                else if (string.IsNullOrEmpty(screenshotPath))
                    screenshotPath = ExtractYamlValue(trimmed.Substring("path:".Length));
                continue;
            }
            if (inLogging && trimmed.StartsWith("level:"))
            {
                logLevel = ExtractYamlValue(trimmed.Substring("level:".Length));
                if (string.IsNullOrWhiteSpace(logLevel))
                    logLevel = "info";
                continue;
            }
            if (inLogging && trimmed.StartsWith("path:"))
            {
                logPath = ExtractYamlValue(trimmed.Substring("path:".Length));
                continue;
            }

            if (trimmed.StartsWith("-") || (trimmed.Length > 0 && char.IsLetter(trimmed[0]) && trimmed.Contains(':')))
                inSkill = inScreenshot = inLogging = false;
        }

        return (skillSave, skillLocal, screenshotPath, logLevel, logPath);
    }

    private static string ExtractYamlValue(string part)
    {
        var v = part.Trim();
        if (v.StartsWith('"') && v.Length >= 2)
        {
            v = v.Substring(1, v.Length - 2);
            return v.Replace("\\\\", "\\").Replace("\\\"", "\"");
        }
        if (v.StartsWith('\'') && v.Length >= 2)
            return v.Substring(1, v.Length - 2);
        return v;
    }

    private void WriteYamlPaths(string skillSavePath, string skillLocalPath, string screenshotPath, string logLevel, string logPath)
    {
        var dir = Path.GetDirectoryName(_yamlPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var lines = File.Exists(_yamlPath) ? File.ReadAllLines(_yamlPath) : Array.Empty<string>();
        var inSkill = false;
        var inScreenshot = false;
        var inLogging = false;
        var result = new System.Collections.Generic.List<string>();
        var quote = (string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("skill:"))
            {
                inSkill = true;
                inScreenshot = inLogging = false;
                result.Add(line);
                continue;
            }
            if (trimmed.StartsWith("screenshot:"))
            {
                inScreenshot = true;
                inSkill = inLogging = false;
                result.Add(line);
                continue;
            }
            if (trimmed.StartsWith("logging:"))
            {
                inLogging = true;
                inSkill = inScreenshot = false;
                result.Add(line);
                continue;
            }

            if (inSkill && trimmed.StartsWith("save_path:"))
            {
                result.Add($"  save_path: {quote(skillSavePath)}");
                continue;
            }
            if (inSkill && trimmed.StartsWith("local_path:"))
            {
                result.Add($"  local_path: {quote(skillLocalPath)}");
                continue;
            }
            if (inScreenshot && trimmed.StartsWith("windows_path:"))
            {
                result.Add($"  windows_path: {quote(screenshotPath)}");
                continue;
            }
            if (inScreenshot && trimmed.StartsWith("path:"))
            {
                result.Add(line);
                continue;
            }
            if (inLogging && trimmed.StartsWith("level:"))
            {
                result.Add($"  level: {logLevel}");
                continue;
            }
            if (inLogging && trimmed.StartsWith("path:"))
            {
                result.Add($"  path: {quote(logPath)}");
                continue;
            }

            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || (trimmed.Length > 0 && char.IsLetter(trimmed[0]) && trimmed.Contains(':')))
                inSkill = inScreenshot = inLogging = false;
            result.Add(line);
        }

        if (!File.Exists(_yamlPath) || result.Count == 0)
        {
            WriteFullDefaultYaml(skillSavePath, skillLocalPath, screenshotPath, logPath);
            return;
        }

        File.WriteAllLines(_yamlPath, result);
    }

    private (string apiKey, string modelName, string apiEndpoint, string repoRoot) ReadJsonApi()
    {
        if (!File.Exists(_jsonPath))
            return (string.Empty, "claude-sonnet-4-20250514", string.Empty, string.Empty);
        try
        {
            var json = File.ReadAllText(_jsonPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var apiKey = root.TryGetProperty("apiKey", out var ak) ? ak.GetString() ?? string.Empty : string.Empty;
            var defaultModel = "claude-sonnet-4-20250514";
            var modelName = root.TryGetProperty("modelName", out var mn) ? mn.GetString() ?? defaultModel : defaultModel;
            var apiEndpoint = root.TryGetProperty("apiEndpoint", out var ae) ? ae.GetString() ?? string.Empty : string.Empty;
            var repoRoot = root.TryGetProperty("repoRoot", out var rr) ? rr.GetString() ?? string.Empty : string.Empty;
            return (apiKey ?? string.Empty, modelName ?? defaultModel, apiEndpoint ?? string.Empty, repoRoot ?? string.Empty);
        }
        catch
        {
            return (string.Empty, "claude-sonnet-4-20250514", string.Empty, string.Empty);
        }
    }

    private void WriteJsonApi(string apiKey, string modelName, string apiEndpoint, string repoRoot)
    {
        var dir = Path.GetDirectoryName(_jsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var o = new { apiKey, modelName, apiEndpoint, repoRoot };
        var json = JsonSerializer.Serialize(o, JsonOptions);
        File.WriteAllText(_jsonPath, json);
    }
}

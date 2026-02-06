using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Shogun.Avalonia.Util;

namespace Shogun.Avalonia.Services;

/// <summary>
/// アプリ内で Node.js と Claude Code CLI を導入する（環境を汚さない）。
/// </summary>
public class ClaudeCodeSetupService : IClaudeCodeSetupService
{
    private const string NodeVersion = "v20.20.0";

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static string PlatformArch => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64 => "arm64",
        _ => "x64"
    };

    /// <summary>node 実行ファイル名（Windows: node.exe, macOS/Linux: node）。</summary>
    private static string NodeExeName => IsWindows ? "node.exe" : "node";

    /// <summary>npm スクリプト名（Windows: npm.cmd, macOS/Linux: npm）。</summary>
    private static string NpmScriptName => IsWindows ? "npm.cmd" : "npm";

    /// <summary>プラットフォーム別アーカイブ名。</summary>
    private static string NodeArchiveName => IsWindows
        ? $"node-{NodeVersion}-win-x64.zip"
        : IsMacOS
            ? $"node-{NodeVersion}-darwin-{PlatformArch}.tar.gz"
            : $"node-{NodeVersion}-linux-{PlatformArch}.tar.gz";

    private static string NodeDownloadUrl => $"https://nodejs.org/download/release/latest-v20.x/{NodeArchiveName}";

    /// <summary>node/npm 等のバイナリが置かれるディレクトリ。Windows: nodeDir 直下、macOS/Linux: nodeDir/bin。</summary>
    private static string GetNodeBinDir(string nodeDir)
    {
        return IsWindows ? nodeDir : Path.Combine(nodeDir, "bin");
    }

    /// <summary>アプリルート（Node/Claude インストール先）。LocalApplicationData\Shogun.Avalonia。アプリルートは node インストール・Claude Code インストール・log4net のログのみに使用。</summary>
    private static string BaseDir
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "Shogun.Avalonia");
        }
    }

    /// <inheritdoc />
    public string GetAppLocalNodeDir()
    {
        var nodeDir = Path.Combine(BaseDir, "nodejs");
        if (!Directory.Exists(nodeDir)) return string.Empty;
        var nodePath = IsWindows
            ? Path.Combine(nodeDir, "node.exe")
            : Path.Combine(nodeDir, "bin", "node");
        return File.Exists(nodePath) ? nodeDir : string.Empty;
    }

    /// <inheritdoc />
    public string GetAppLocalNpmPrefix() => Path.Combine(BaseDir, "npm");

    /// <inheritdoc />
    public string GetNodeExePath()
    {
        var nodeDir = GetAppLocalNodeDir();
        if (string.IsNullOrEmpty(nodeDir)) return string.Empty;
        return IsWindows
            ? Path.Combine(nodeDir, "node.exe")
            : Path.Combine(nodeDir, "bin", "node");
    }

    /// <inheritdoc />
    public bool IsNodeInstalled() => !string.IsNullOrEmpty(GetAppLocalNodeDir());

    /// <inheritdoc />
    public bool IsClaudeCodeInstalled()
    {
        if (!IsNodeInstalled())
            return false;
        var path = GetClaudeExecutablePath();
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>インストール済みの Node.js バージョンを取得する。</summary>
    private string GetInstalledNodeVersion()
    {
        var nodeExe = GetNodeExePath();
        if (string.IsNullOrEmpty(nodeExe) || !File.Exists(nodeExe)) return string.Empty;

        try
        {
            using var proc = new Process();
            proc.StartInfo.FileName = nodeExe;
            proc.StartInfo.Arguments = "-v";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output; // 例: "v20.20.0"
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<bool> InstallNodeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        // バージョンチェック
        var currentVersion = GetInstalledNodeVersion();
        if (currentVersion == NodeVersion)
        {
            Logger.Log($"Node.js は既に指定バージョン {NodeVersion} です。ダウンロードをスキップします。", LogLevel.Info);
            progress?.Report($"Node.js {NodeVersion} はインストール済みです。");
            return true;
        }

        var baseDir = BaseDir;
        var extractDir = Path.Combine(baseDir, "nodejs_extract");
        var nodeDir = Path.Combine(baseDir, "nodejs");
        var archivePath = Path.Combine(baseDir, NodeArchiveName);
        try
        {
            Logger.Log($"Node.js のインストールを開始します (Target: {NodeVersion}, Current: {currentVersion}, Archive: {NodeArchiveName})", LogLevel.Info);
            progress?.Report($"Node.js {NodeVersion} をダウンロード中…");
            Directory.CreateDirectory(baseDir);
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                var bytes = await client.GetByteArrayAsync(NodeDownloadUrl, cancellationToken).ConfigureAwait(false);
                await File.WriteAllBytesAsync(archivePath, bytes, cancellationToken).ConfigureAwait(false);
            }
            progress?.Report("Node.js を展開中…");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);

            if (IsWindows)
            {
                ZipFile.ExtractToDirectory(archivePath, extractDir);
            }
            else
            {
                // macOS/Linux: tar.gz を展開
                using var fileStream = File.OpenRead(archivePath);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(gzipStream, extractDir, overwriteFiles: true, cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists(nodeDir))
                Directory.Delete(nodeDir, true);
            var innerDir = Directory.GetDirectories(extractDir)
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith("node-", StringComparison.Ordinal));
            if (string.IsNullOrEmpty(innerDir))
            {
                progress?.Report("展開後のフォルダが見つかりません。");
                return false;
            }
            Directory.Move(innerDir, nodeDir);
            Directory.Delete(extractDir, false);
            try { File.Delete(archivePath); } catch { /* ignore */ }
            Logger.Log("Node.js のインストールが完了しました。", LogLevel.Info);
            progress?.Report("Node.js のインストールが完了しました。");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException("Node.js のインストールに失敗しました。", ex);
            progress?.Report($"エラー: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> InstallClaudeCodeAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var nodeDir = GetAppLocalNodeDir();
        if (string.IsNullOrEmpty(nodeDir))
        {
            progress?.Report("先に Node.js をインストールしてください。");
            return false;
        }
        var npmPrefix = GetAppLocalNpmPrefix();
        Directory.CreateDirectory(npmPrefix);
        var nodeBinDir = GetNodeBinDir(nodeDir);
        var npmPath = Path.Combine(nodeBinDir, NpmScriptName);
        if (!File.Exists(npmPath))
        {
            progress?.Report("npm が見つかりません。");
            return false;
        }
        try
        {
            Logger.Log("Claude Code CLI のインストールを開始します。", LogLevel.Info);
            progress?.Report("Claude Code CLI をインストール中…");
            var startInfo = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = "install -g @anthropic-ai/claude-code",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = nodeDir
            };
            startInfo.Environment["NPM_CONFIG_PREFIX"] = npmPrefix;
            startInfo.Environment["PATH"] = nodeBinDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
            using var proc = Process.Start(startInfo);
            if (proc == null)
            {
                progress?.Report("プロセスを開始できませんでした。");
                return false;
            }
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                Logger.Log($"npm が終了コード {proc.ExitCode} で終了しました。", LogLevel.Warning);
                progress?.Report($"npm が終了コード {proc.ExitCode} で終了しました。");
                return false;
            }
            Logger.Log("Claude Code CLI のインストールが完了しました。", LogLevel.Info);
            progress?.Report("Claude Code CLI のインストールが完了しました。");
            return IsClaudeCodeInstalled();
        }
        catch (Exception ex)
        {
            Logger.LogException("Claude Code CLI のインストールに失敗しました。", ex);
            progress?.Report($"エラー: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public string GetClaudeExecutablePath()
    {
        var prefix = GetAppLocalNpmPrefix();
        if (IsWindows)
        {
            var claudeCmd = Path.Combine(prefix, "claude.cmd");
            if (File.Exists(claudeCmd))
                return claudeCmd;
            var binClaude = Path.Combine(prefix, "node_modules", ".bin", "claude.cmd");
            return File.Exists(binClaude) ? binClaude : string.Empty;
        }
        else
        {
            // macOS/Linux: npm -g は prefix/bin/ にシンボリックリンクを作成する
            var claudeBin = Path.Combine(prefix, "bin", "claude");
            if (File.Exists(claudeBin))
                return claudeBin;
            var binClaude = Path.Combine(prefix, "node_modules", ".bin", "claude");
            return File.Exists(binClaude) ? binClaude : string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RunLoginAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var nodeExe = GetNodeExePath();
        var claudeJsPath = Path.Combine(GetAppLocalNpmPrefix(), "node_modules", "@anthropic-ai", "claude-code", "cli.js");

        if (string.IsNullOrEmpty(nodeExe) || !File.Exists(nodeExe) || !File.Exists(claudeJsPath))
        {
            progress?.Report("Node.js または Claude Code が見つかりません。先にインストールを実行してください。");
            return false;
        }

        try
        {
            progress?.Report("ログイン用のウィンドウを開いています。認証を完了してください...");
            var nodeBinDir = Path.GetDirectoryName(nodeExe)!;

            ProcessStartInfo startInfo;
            if (IsWindows)
            {
                // Windows: 新しいコンソールウィンドウを開き、PATH を通した状態でログインを実行する
                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    // /c の後の全体を二重引用符で囲むことで、内部のパスにスペースが含まれていても正しく解釈させる
                    Arguments = $"/c \"set \"PATH={nodeBinDir};%PATH%\" && \"{nodeExe}\" \"{claudeJsPath}\" login || pause\"",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
            }
            else if (IsMacOS)
            {
                // macOS: .command ファイルを作成し、Terminal.app で開く
                var scriptPath = Path.Combine(Path.GetTempPath(), $"shogun_claude_login_{Environment.ProcessId}.command");
                var script = $"#!/bin/bash\nexport PATH=\"{nodeBinDir}:$PATH\"\n\"{nodeExe}\" \"{claudeJsPath}\" login\necho \"\"\nread -p \"Press Enter to close...\"";
                await File.WriteAllTextAsync(scriptPath, script, cancellationToken).ConfigureAwait(false);
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                chmod?.WaitForExit(3000);
                startInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
            }
            else
            {
                // Linux: bash で直接実行
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c 'export PATH=\"{nodeBinDir}:$PATH\" && \"{nodeExe}\" \"{claudeJsPath}\" login'",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                };
            }

            Logger.Log($"ログインウィンドウを起動します: {startInfo.FileName} {startInfo.Arguments}", LogLevel.Info);
            using var proc = Process.Start(startInfo);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException("ログインプロセスの起動に失敗しました。", ex);
            progress?.Report($"エラー: {ex.Message}");
            return false;
        }
    }

    /// <summary>テスト用の最小プロンプト（ログイン確認に使用）。</summary>
    private const string LoginTestPrompt = "reply with exactly: ok";

    /// <inheritdoc />
    public async Task<bool> VerifyClaudeCodeConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var nodeExe = GetNodeExePath();
        var claudeJsPath = Path.Combine(GetAppLocalNpmPrefix(), "node_modules", "@anthropic-ai", "claude-code", "cli.js");

        if (string.IsNullOrEmpty(nodeExe) || !File.Exists(nodeExe) || !File.Exists(claudeJsPath))
        {
            Logger.Log("Claude Code 疎通確認: 実行ファイルが見つかりません。", LogLevel.Warning);
            return false;
        }

        try
        {
            Logger.Log("Claude Code の疎通確認を実行します（node cli.js --version）。", LogLevel.Debug);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            using var proc = new Process();
            proc.StartInfo.FileName = nodeExe;
            proc.StartInfo.Arguments = $"\"{claudeJsPath}\" --version";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var nodeBinDir = Path.GetDirectoryName(nodeExe)!;
            proc.StartInfo.Environment["PATH"] = nodeBinDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";

            proc.Start();
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            var ok = proc.ExitCode == 0;
            Logger.Log(ok ? "Claude Code の疎通確認に成功しました。" : $"Claude Code の疎通確認に失敗しました（終了コード: {proc.ExitCode}）。", ok ? LogLevel.Info : LogLevel.Warning);
            return ok;
        }
        catch (Exception ex)
        {
            Logger.LogException("Claude Code の疎通確認で例外が発生しました。", ex);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken = default)
    {
        var nodeExe = GetNodeExePath();
        var claudeJsPath = Path.Combine(GetAppLocalNpmPrefix(), "node_modules", "@anthropic-ai", "claude-code", "cli.js");

        if (string.IsNullOrEmpty(nodeExe) || !File.Exists(nodeExe) || !File.Exists(claudeJsPath))
        {
            Logger.Log("ログイン確認: 実行ファイルが見つかりません。", LogLevel.Warning);
            return false;
        }

        try
        {
            Logger.Log("Claude Code のログイン確認を実行します（config get）。", LogLevel.Debug);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(20));

            using var proc = new Process();
            proc.StartInfo.FileName = nodeExe;
            // config get はログインしていないと失敗するが、AI 呼び出し（トークン消費）は行わない
            proc.StartInfo.Arguments = $"\"{claudeJsPath}\" config get";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 非対話環境フラグ
            proc.StartInfo.Environment["CI"] = "true";
            var nodeBinDir = Path.GetDirectoryName(nodeExe)!;
            proc.StartInfo.Environment["PATH"] = nodeBinDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";

            proc.Start();
            proc.StandardInput.Close();

            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            // ログインしていれば ExitCode 0、していなければエラー（1など）が返る
            var ok = proc.ExitCode == 0;
            Logger.Log(ok ? "Claude Code のログイン確認に成功しました。" : $"Claude Code のログイン確認に失敗しました（終了コード: {proc.ExitCode}）。", ok ? LogLevel.Info : LogLevel.Warning);
            return ok;
        }
        catch (Exception ex)
        {
            Logger.LogException("Claude Code のログイン確認で例外が発生しました。", ex);
            return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>順序は依存関係に基づく: (1) Node.js → (2) Claude Code → 呼び出し元で (3) ログイン確認。Claude Code は npm で入れるため Node が先。ログイン確認は claude コマンドが必要なため Claude Code が先。</remarks>
    public async Task EnsureClaudeCodeEnvironmentAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Logger.Log("Claude Code 環境の確保を開始します。", LogLevel.Info);
        // (1) Node.js: 基盤。未導入ならインストール
        progress?.Report("Node.js を確認しています...");
        if (!IsNodeInstalled())
        {
            Logger.Log("Node.js が未導入のためインストールを実行します。", LogLevel.Info);
            progress?.Report("Node.js をインストールしています...");
            await InstallNodeAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Logger.Log("Node.js はインストール済みです。最新版への更新を試みます。", LogLevel.Debug);
            progress?.Report("Node.js を更新しています...");
            // Node.js の再インストール（上書き）で更新を試みる
            await InstallNodeAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        // (2) Claude Code: Node の上に npm で導入。常に最新版をインストール（npm install -g は最新を入れる）
        progress?.Report("Claude Code を確認・更新しています...");
        Logger.Log("Claude Code のインストール/更新を実行します。", LogLevel.Info);
        progress?.Report("Claude Code をインストール/更新しています...");
        await InstallClaudeCodeAsync(progress, cancellationToken).ConfigureAwait(false);

        Logger.Log("Claude Code 環境の確保が完了しました。", LogLevel.Info);
    }
}

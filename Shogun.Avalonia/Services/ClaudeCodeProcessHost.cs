using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shogun.Avalonia.Util;

namespace Shogun.Avalonia.Services;

/// <summary>
/// 将軍・家老・足軽の Claude Code CLI を常駐プロセスで起動し、ジョブごとに終了させない。
/// プロセスの終了は <see cref="StopAll"/> 呼び出し（アプリ終了時）のみ。
/// </summary>
public class ClaudeCodeProcessHost : IClaudeCodeProcessHost
{
    private static readonly string RunnerScriptContent = GetRunnerScriptContent();

    private readonly IClaudeCodeSetupService _setupService;
    private readonly IShogunQueueService _queueService;
    private readonly Dictionary<string, ProcessEntry> _processes = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _started;

    /// <summary>サービスを生成する。</summary>
    public ClaudeCodeProcessHost(IClaudeCodeSetupService setupService, IShogunQueueService queueService)
    {
        _setupService = setupService;
        _queueService = queueService;
    }

    /// <inheritdoc />
    public async Task StartAllAsync(Func<string, string, Task>? onProcessReady = null, CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
                return;
            var nodeDir = _setupService.GetAppLocalNodeDir();
            if (string.IsNullOrEmpty(nodeDir))
            {
                Logger.Log("ClaudeCodeProcessHost: Node.js がインストールされていません。", LogLevel.Error);
                return;
            }
            var nodeExe = Path.Combine(nodeDir, "node.exe");
            if (!File.Exists(nodeExe))
            {
                Logger.Log($"ClaudeCodeProcessHost: node.exe が見つかりません: {nodeExe}", LogLevel.Error);
                return;
            }
            var cliJs = Path.Combine(_setupService.GetAppLocalNpmPrefix(), "node_modules", "@anthropic-ai", "claude-code", "cli.js");
            if (!File.Exists(cliJs))
            {
                Logger.Log($"ClaudeCodeProcessHost: cli.js が見つかりません: {cliJs}", LogLevel.Error);
                return;
            }
            var repoRoot = _queueService.GetRepoRoot();
            if (string.IsNullOrEmpty(repoRoot) || !Directory.Exists(repoRoot))
            {
                Logger.Log("ClaudeCodeProcessHost: ワークスペースルートが無効です。", LogLevel.Error);
                return;
            }
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shogun.Avalonia");
            Directory.CreateDirectory(baseDir);
            var runnerPath = Path.Combine(baseDir, "agent-runner.js");
            await File.WriteAllTextAsync(runnerPath, RunnerScriptContent, cancellationToken).ConfigureAwait(false);

            var roles = new List<string> { "将軍", "家老" };
            var ashigaruCount = _queueService.GetAshigaruCount();
            for (var i = 1; i <= ashigaruCount; i++)
                roles.Add($"足軽{i}");

            var env = new Dictionary<string, string>
            {
                ["RUNNER_NODE_EXE"] = nodeExe,
                ["RUNNER_CLI_JS"] = cliJs,
                ["RUNNER_CWD"] = repoRoot,
                ["CI"] = "true",
                ["NO_COLOR"] = "true",
                ["TERM"] = "dumb",
                ["FORCE_COLOR"] = "0",
                ["NODE_OPTIONS"] = "--no-deprecation",
                ["DEBIAN_FRONTEND"] = "noninteractive",
                ["GITHUB_ACTIONS"] = "true",
                ["PATH"] = nodeDir + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "")
            };

            foreach (var role in roles)
            {
                var entry = StartRunnerProcess(nodeExe, runnerPath, env);
                if (entry != null)
                {
                    _processes[role] = entry;
                    if (onProcessReady != null)
                        await onProcessReady(role, "✓ プロセス起動完了").ConfigureAwait(false);
                }
            }
            _started = true;
            Logger.Log($"ClaudeCodeProcessHost: 常駐プロセスを {_processes.Count} ロールで起動しました。", LogLevel.Info);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Output)> RunJobAsync(
        string roleLabel,
        string userPrompt,
        string systemPromptPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!_processes.TryGetValue(roleLabel, out var entry))
        {
            progress?.Report($"{roleLabel} の常駐プロセスがありません。");
            return (false, string.Empty);
        }

        var job = new { prompt = userPrompt, systemPromptFile = systemPromptPath };
        var line = JsonSerializer.Serialize(job) + "\n";
        var tcs = new TaskCompletionSource<(bool Success, string Output)>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (entry.Lock)
        {
            if (entry.CurrentTcs != null)
            {
                progress?.Report($"{roleLabel} は別ジョブ実行中です。");
                return (false, string.Empty);
            }
            entry.CurrentTcs = tcs;
            entry.CurrentProgress = progress;
        }

        try
        {
            entry.StdinWriter.Write(line);
            await entry.StdinWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(120)); // 120秒のタイムアウト
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return (false, "タイムアウト");
        }
        finally
        {
            lock (entry.Lock)
            {
                entry.CurrentTcs = null;
                entry.CurrentProgress = null;
            }
        }
    }

    /// <inheritdoc />
    public void StopAll()
    {
        _initLock.Wait();
        try
        {
            foreach (var kv in _processes)
            {
                try
                {
                    if (kv.Value.Process.HasExited)
                        continue;
                    kv.Value.Process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    Logger.Log($"ClaudeCodeProcessHost: プロセス終了時に例外: {kv.Key}, {ex.Message}", LogLevel.Warning);
                }
            }
            _processes.Clear();
            _started = false;
            Logger.Log("ClaudeCodeProcessHost: 全常駐プロセスを終了しました。", LogLevel.Info);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static ProcessEntry? StartRunnerProcess(string nodeExe, string runnerPath, Dictionary<string, string> env)
    {
        try
        {
            var proc = new Process();
            proc.StartInfo.FileName = nodeExe;
            proc.StartInfo.Arguments = "\"" + runnerPath.Replace("\"", "\\\"") + "\"";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.StandardInputEncoding = Encoding.UTF8;
            proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            proc.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            foreach (var kv in env)
                proc.StartInfo.Environment[kv.Key] = kv.Value;
            proc.Start();
            var stdinWriter = new StreamWriter(proc.StandardInput.BaseStream, Encoding.UTF8) { AutoFlush = false };
            var stdout = proc.StandardOutput;
            var entry = new ProcessEntry(proc, stdinWriter);
            entry.StdoutReaderTask = Task.Run(() => ReadStdoutLoopAsync(entry, stdout));
            return entry;
        }
        catch (Exception ex)
        {
            Logger.LogException("ClaudeCodeProcessHost: ランプロセス起動失敗", ex);
            return null;
        }
    }

    private static async Task ReadStdoutLoopAsync(ProcessEntry entry, StreamReader stdout)
    {
        try
        {
            string? line;
            while ((line = await stdout.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                TaskCompletionSource<(bool Success, string Output)>? tcs;
                IProgress<string>? progress;
                lock (entry.Lock)
                {
                    tcs = entry.CurrentTcs;
                    progress = entry.CurrentProgress;
                }

                if (line.StartsWith("OUT:", StringComparison.Ordinal))
                {
                    var msg = line.Length > 4 ? line.Substring(4) : "";
                    progress?.Report(msg);
                }
                else if (line.StartsWith("RESULT:", StringComparison.Ordinal))
                {
                    var json = line.Length > 7 ? line.Substring(7) : "{}";
                    bool success = false;
                    string output = "";
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var exitCode = root.TryGetProperty("exitCode", out var ec) ? ec.GetInt32() : 1;
                        output = root.TryGetProperty("output", out var o) ? o.GetString() ?? "" : "";
                        success = (exitCode == 0);
                    }
                    catch
                    {
                        output = json;
                        success = false;
                    }
                    tcs?.TrySetResult((success, output));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Logger.Log($"ClaudeCodeProcessHost: stdout 読み取り例外: {ex.Message}", LogLevel.Warning);
            lock (entry.Lock)
            {
                entry.CurrentTcs?.TrySetResult((false, ex.Message));
            }
        }
    }

    private static string GetRunnerScriptContent()
    {
        return """
const { spawn } = require('child_process');
const readline = require('readline');

const nodeExe = process.env.RUNNER_NODE_EXE;
const cliJs = process.env.RUNNER_CLI_JS;
const cwd = process.env.RUNNER_CWD;

if (!nodeExe || !cliJs || !cwd) {
  const msg = `RUNNER env missing: nodeExe=${nodeExe}, cliJs=${cliJs}, cwd=${cwd}`;
  process.stdout.write('RESULT:' + JSON.stringify({ exitCode: 1, output: msg }) + '\n');
  process.exit(1);
}

const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });

rl.on('line', (line) => {
  if (!line.trim()) return;
  
  let job;
  try {
    job = JSON.parse(line);
  } catch (parseErr) {
    process.stdout.write('RESULT:' + JSON.stringify({ exitCode: 1, output: `JSON parse error: ${parseErr.message}` }) + '\n');
    return;
  }

  const prompt = job.prompt || '';
  const systemPromptFile = job.systemPromptFile || '';

  let childStarted = false;
  let stderrOutput = '';
  try {
    const child = spawn(nodeExe, [cliJs, '-p', prompt, '--append-system-prompt-file', systemPromptFile], {
      cwd,
      stdio: ['pipe', 'pipe', 'pipe'],
      timeout: 60000
    });
    childStarted = true;

    // Close stdin immediately since we're using -p (print mode)
    child.stdin?.end();

    const chunks = [];
    let buf = '';

    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (data) => {
      buf += data.toString();
      let idx;
      while ((idx = buf.indexOf('\n')) !== -1) {
        const line2 = buf.slice(0, idx);
        buf = buf.slice(idx + 1);
        if (line2.length > 0) chunks.push(line2);
        process.stdout.write('OUT:' + line2 + '\n');
      }
    });

    child.stderr.on('data', (data) => {
      const errStr = data.toString();
      if (errStr.trim()) {
        stderrOutput += errStr;
        process.stdout.write('OUT:[stderr] ' + errStr.trim() + '\n');
      }
    });

    child.on('error', (err) => {
      const errMsg = `Child spawn error: ${err.message}`;
      process.stdout.write('RESULT:' + JSON.stringify({ exitCode: 1, output: errMsg }) + '\n');
    });

    child.on('close', (code) => {
      if (buf.length > 0) {
        chunks.push(buf);
        process.stdout.write('OUT:' + buf + '\n');
      }
      const fullOutput = chunks.join('\n');
      if (code !== 0 && stderrOutput) {
        process.stdout.write('RESULT:' + JSON.stringify({ exitCode: code || 1, output: `STDERR: ${stderrOutput}\nSTDOUT: ${fullOutput}` }) + '\n');
      } else {
        process.stdout.write('RESULT:' + JSON.stringify({ exitCode: code || 0, output: fullOutput }) + '\n');
      }
    });
  } catch (spawnErr) {
    const errMsg = `Spawn error: ${spawnErr.message}`;
    process.stdout.write('RESULT:' + JSON.stringify({ exitCode: 1, output: errMsg }) + '\n');
  }
});

rl.on('close', () => {
  process.exit(0);
});
""";
    }

    private sealed class ProcessEntry
    {
        internal Process Process { get; }
        internal StreamWriter StdinWriter { get; }
        internal Task? StdoutReaderTask { get; set; }
        internal readonly object Lock = new();
        internal TaskCompletionSource<(bool Success, string Output)>? CurrentTcs;
        internal IProgress<string>? CurrentProgress;

        internal ProcessEntry(Process process, StreamWriter stdinWriter)
        {
            Process = process;
            StdinWriter = stdinWriter;
        }
    }
}

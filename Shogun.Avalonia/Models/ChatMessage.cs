using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Shogun.Avalonia.Models;

/// <summary>
/// チャットメッセージモデル。
/// </summary>
public partial class ChatMessage : ObservableObject
{
    /// <summary>送信者（user, ai, system）。</summary>
    [ObservableProperty]
    private string _sender = string.Empty;

    /// <summary>メッセージ内容。</summary>
    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>タイムスタンプ。</summary>
    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    /// <summary>関連するプロジェクトID。</summary>
    [ObservableProperty]
    private string _projectId = string.Empty;

    /// <summary>表示用の文字列（送信者: 内容）。</summary>
    public string DisplayText => $"[{ProjectId}] {Sender}: {Content}";
}

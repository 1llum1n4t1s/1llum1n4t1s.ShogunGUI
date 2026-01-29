using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Shogun.Avalonia.Models;

/// <summary>
/// エージェントペイン内の1ブロック（指示・報告・ステータス等）。
/// </summary>
public partial class PaneBlock : ObservableObject
{
    /// <summary>表示テキスト（本文）。</summary>
    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>ステータス行（例: * Baked for 1m 50s, * Doing..., * Crunched for 1m 25s）。</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>タイムスタンプ（任意）。</summary>
    [ObservableProperty]
    private DateTime? _timestamp;
}

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shogun.Avalonia.Services;
using Shogun.Avalonia.ViewModels;

namespace Shogun.Avalonia;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService = new SettingsService();
    private readonly IProjectService _projectService = new ProjectService();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow();
        w.DataContext = new SettingsViewModel(_settingsService, () =>
        {
            w.Close();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.RefreshAiService();
            }
        });
        w.Show();
    }

    private void OnProjectSettingsClick(object? sender, RoutedEventArgs e)
    {
        var w = new ProjectSettingsWindow();
        w.DataContext = new ProjectSettingsViewModel(_projectService, () =>
        {
            w.Close();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.LoadProjects();
            }
        });
        w.Show();
    }

    private async void OnChatInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm && !vm.IsAiProcessing)
        {
            await vm.SendMessageAsync();
            e.Handled = true;
        }
    }

    private async void OnSendButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && !vm.IsAiProcessing)
        {
            await vm.SendMessageAsync();
        }
    }
}

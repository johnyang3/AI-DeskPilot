using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DeskPilot.Models;
using DeskPilot.Services;

namespace DeskPilot;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ProtectionService _protection = new();
    private readonly ArchiveService _archive = new();
    private readonly DesktopService _desktop;
    private readonly CleanerService _cleaner;
    private readonly RecycleService _recycle;
    private readonly UpdateService _update = new();
    private readonly AiService _ai = new();

    private AppSettings _settings;
    private readonly ObservableCollection<string> _archives = new();
    private readonly ObservableCollection<CleanupCandidate> _candidates = new();
    private List<DesktopMove> _plan = new();

    public MainWindow()
    {
        InitializeComponent();

        _settings = _settingsService.Load();
        _desktop = new DesktopService(_settingsService, _protection);
        _cleaner = new CleanerService(_settingsService);
        _recycle = new RecycleService(_protection);

        lstArchives.ItemsSource = _archives;
        lstCandidates.ItemsSource = _candidates;

        LoadSettingsToUi();
        ApplyLocalization();
        WireEvents();

        txtVersion.Text = $"{Loc.T("Version")}: {UpdateService.CurrentVersion}";
        AppendChat("Assistant", Loc.T("AiWelcome"));
    }

    #region Setup

    private void LoadSettingsToUi()
    {
        txtEndpoint.Text = _settings.ApiEndpoint;
        txtModel.Text = _settings.Model;
        txtApiKey.Password = _settingsService.GetApiKey(_settings);
        txtProtectedPaths.Text = string.Join(Environment.NewLine, _settings.ProtectedPaths);
        txtOutput.Text = _settings.LastOutputDirectory;
    }

    private void ApplyLocalization()
    {
        Title = Loc.T("AppTitle");
        TabArchives.Header = Loc.T("Archives");
        TabDesktop.Header = Loc.T("Desktop");
        TabCleanup.Header = Loc.T("Cleanup");
        TabAssistant.Header = Loc.T("Assistant");
        TabSettings.Header = Loc.T("Settings");

        btnAddArchives.Content = Loc.T("AddArchives");
        btnRemoveArchives.Content = Loc.T("RemoveSelected");
        btnChooseOutput.Content = Loc.T("ChooseOutput");
        btnExtract.Content = Loc.T("StartExtract");

        btnPreviewOrganize.Content = Loc.T("PreviewOrganize");
        btnApplyOrganize.Content = Loc.T("ApplyOrganize");
        btnUndoOrganize.Content = Loc.T("UndoOrganize");
        lblDesktopHint.Text = Loc.T("OrganizeHint");

        btnScan.Content = Loc.T("Scan");
        btnCleanSelected.Content = Loc.T("CleanSelected");
        lblCleanupHint.Text = Loc.T("CleanupHint");

        btnSend.Content = Loc.T("Send");

        btnSaveAi.Content = Loc.T("SaveAi");
        btnSaveProtection.Content = Loc.T("SaveProtection");
        btnCheckUpdate.Content = Loc.T("CheckUpdate");
        btnOpenRepo.Content = Loc.T("OpenRepo");
    }

    private void WireEvents()
    {
        btnAddArchives.Click += OnAddArchives;
        btnRemoveArchives.Click += OnRemoveArchives;
        btnChooseOutput.Click += OnChooseOutput;
        btnExtract.Click += OnExtract;

        btnPreviewOrganize.Click += OnPreviewOrganize;
        btnApplyOrganize.Click += OnApplyOrganize;
        btnUndoOrganize.Click += OnUndoOrganize;

        btnScan.Click += OnScan;
        btnCleanSelected.Click += OnCleanSelected;

        btnSend.Click += OnSendAi;
        txtUserInput.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) OnSendAi(this, e); };

        btnSaveAi.Click += OnSaveAi;
        btnSaveProtection.Click += OnSaveProtection;
        btnCheckUpdate.Click += OnCheckUpdate;
        btnOpenRepo.Click += OnOpenRepo;
    }

    #endregion

    #region Archive

    private void OnAddArchives(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = Loc.T("AddArchives"),
            Filter = "Archives|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz|All files|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var file in dialog.FileNames)
        {
            if (!_archives.Contains(file, StringComparer.OrdinalIgnoreCase)) _archives.Add(file);
        }
    }

    private void OnRemoveArchives(object sender, RoutedEventArgs e)
    {
        var selected = lstArchives.SelectedItems.Cast<string>().ToList();
        foreach (var item in selected) _archives.Remove(item);
    }

    private void OnChooseOutput(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("ChooseOutput") };
        if (dialog.ShowDialog(this) != true) return;
        txtOutput.Text = dialog.FolderName;
        _settings.LastOutputDirectory = dialog.FolderName;
        _settingsService.Save(_settings);
    }

    private async void OnExtract(object sender, RoutedEventArgs e)
    {
        if (_archives.Count == 0)
        {
            MessageBox.Show(this, Loc.T("NoArchives"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtOutput.Text))
        {
            MessageBox.Show(this, Loc.T("ChooseOutput"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Confirm(Loc.F("ConfirmExtract", _archives.Count, txtOutput.Text))) return;

        btnExtract.IsEnabled = false;
        try
        {
            var result = await _archive.ExtractManyAsync(_archives, txtOutput.Text.Trim());
            txtArchiveLog.Text = result.Log;
            MessageBox.Show(this, Loc.F("ExtractComplete", result.Succeeded, result.Failed),
                Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(this, Loc.T("OperationCancelled"), Loc.T("Cancel"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{Loc.T("UpdateFailed")} {ex.Message}", Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnExtract.IsEnabled = true;
        }
    }

    #endregion

    #region Desktop

    private void OnPreviewOrganize(object sender, RoutedEventArgs e)
    {
        _plan = _desktop.CreatePlan(_settings);
        if (_plan.Count == 0)
        {
            txtDesktopPlan.Text = Loc.T("NoMoves");
            return;
        }
        txtDesktopPlan.Text = string.Join(Environment.NewLine,
            _plan.Select(m => $"{Path.GetFileName(m.Source)}  ->  {m.Destination}"));
    }

    private async void OnApplyOrganize(object sender, RoutedEventArgs e)
    {
        if (_plan.Count == 0) _plan = _desktop.CreatePlan(_settings);
        if (_plan.Count == 0)
        {
            MessageBox.Show(this, Loc.T("NoMoves"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var preview = string.Join(Environment.NewLine, _plan.Take(20).Select(m => m.Source));
        if (_plan.Count > 20) preview += Environment.NewLine + $"… (+{_plan.Count - 20})";
        if (!Confirm(Loc.F("ConfirmMoves", _plan.Count, preview))) return;

        var moved = _desktop.ApplyPlan(_plan);
        MessageBox.Show(this, Loc.F("MoveComplete", moved), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
        _plan = new List<DesktopMove>();
        txtDesktopPlan.Text = string.Empty;
        await Task.CompletedTask;
    }

    private void OnUndoOrganize(object sender, RoutedEventArgs e)
    {
        var restored = _desktop.UndoLast();
        if (restored == 0)
        {
            MessageBox.Show(this, Loc.T("NoUndo"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show(this, Loc.F("UndoComplete", restored), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Cleanup

    private async void OnScan(object sender, RoutedEventArgs e)
    {
        btnScan.IsEnabled = false;
        try
        {
            _candidates.Clear();
            foreach (var candidate in await _cleaner.ScanAsync()) _candidates.Add(candidate);
            MessageBox.Show(this, Loc.F("ScanComplete", _candidates.Count),
                Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{Loc.T("UpdateFailed")} {ex.Message}", Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnScan.IsEnabled = true;
        }
    }

    private void OnCleanSelected(object sender, RoutedEventArgs e)
    {
        var selected = _candidates.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, Loc.T("NothingSelected"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!Confirm(Loc.F("ConfirmCleanup", selected.Count))) return;

        var failures = new List<string>();
        foreach (var candidate in selected)
        {
            try
            {
                if (candidate.Kind == "Program" && !string.IsNullOrWhiteSpace(candidate.UninstallString))
                {
                    UninstallService.Start(candidate.UninstallString);
                }
                else if (!string.IsNullOrWhiteSpace(candidate.Path))
                {
                    _recycle.Recycle(candidate.Path, _settings);
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{candidate.Name}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, failures), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(this, Loc.T("Done"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);

        var cleared = _candidates.Where(c => c.IsSelected).ToList();
        foreach (var c in cleared) _candidates.Remove(c);
    }

    #endregion

    #region Assistant

    private async void OnSendAi(object sender, RoutedEventArgs e)
    {
        var text = txtUserInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var apiKey = _settingsService.GetApiKey(_settings);
        if (string.IsNullOrWhiteSpace(_settings.ApiEndpoint) ||
            string.IsNullOrWhiteSpace(_settings.Model) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this, Loc.T("MissingApi"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppendChat("You", text);
        txtUserInput.Clear();
        btnSend.IsEnabled = false;
        try
        {
            var reply = await _ai.SendAsync(_settings, apiKey, text, ToolHandlerAsync);
            AppendChat("Assistant", string.IsNullOrWhiteSpace(reply) ? "…" : reply);
        }
        catch (Exception ex)
        {
            AppendChat("Assistant", $"{Loc.T("Error")}: {ex.Message}");
        }
        finally
        {
            btnSend.IsEnabled = true;
        }
    }

    private async Task<string> ToolHandlerAsync(string toolName, JsonElement arguments)
    {
        switch (toolName)
        {
            case "get_system_info":
                return await Task.FromResult(GetSystemInfo());

            case "list_desktop_files":
                return await Task.FromResult(string.Join(Environment.NewLine,
                    Directory.EnumerateFiles(_desktop.DesktopPath).Select(Path.GetFileName)));

            case "scan_cleanup_candidates":
                var scanned = await _cleaner.ScanAsync();
                _candidates.Clear();
                foreach (var c in scanned) _candidates.Add(c);
                return JsonSerializer.Serialize(scanned.Select(c => new { c.Name, c.Kind, c.Reason, c.Path }));

            case "organize_desktop":
                if (!Confirm(Loc.T("ConfirmTitle"))) return Loc.T("OperationCancelled");
                var plan = _desktop.CreatePlan(_settings);
                var moved = _desktop.ApplyPlan(plan);
                return Loc.F("MoveComplete", moved);

            case "extract_archives":
            {
                var paths = arguments.TryGetProperty("paths", out var p) && p.ValueKind == JsonValueKind.Array
                    ? p.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : new List<string>();
                var output = arguments.TryGetProperty("output_directory", out var o) ? o.GetString() ?? string.Empty : string.Empty;
                if (paths.Count == 0 || string.IsNullOrWhiteSpace(output)) return "Missing paths or output directory.";
                if (!Confirm(Loc.F("ConfirmExtract", paths.Count, output))) return Loc.T("OperationCancelled");
                var result = await _archive.ExtractManyAsync(paths, output);
                return Loc.F("ExtractComplete", result.Succeeded, result.Failed);
            }

            case "recycle_files":
            {
                var paths = arguments.TryGetProperty("paths", out var rp) && rp.ValueKind == JsonValueKind.Array
                    ? rp.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : new List<string>();
                if (paths.Count == 0) return "No paths provided.";
                if (!Confirm(Loc.F("ConfirmRecycle", paths.Count, string.Join(Environment.NewLine, paths)))) return Loc.T("OperationCancelled");
                var errors = new List<string>();
                foreach (var path in paths)
                {
                    try { _recycle.Recycle(path, _settings); }
                    catch (Exception ex) { errors.Add($"{path}: {ex.Message}"); }
                }
                return errors.Count == 0 ? Loc.T("Done") : string.Join(Environment.NewLine, errors);
            }

            case "uninstall_program":
            {
                var name = arguments.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var target = _candidates.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (target is null || string.IsNullOrWhiteSpace(target.UninstallString)) return Loc.T("NoCandidate");
                if (!Confirm(Loc.T("ConfirmTitle"))) return Loc.T("OperationCancelled");
                UninstallService.Start(target.UninstallString);
                return Loc.F("UninstallStarted", target.Name);
            }

            default:
                return $"Unknown tool: {toolName}";
        }
    }

    private string GetSystemInfo()
    {
        return string.Join(Environment.NewLine,
            $"OS: {Environment.OSVersion}",
            $"Machine: {Environment.MachineName}",
            $"User: {Environment.UserName}",
            $"64-bit: {Environment.Is64BitOperatingSystem}",
            $"Version: {UpdateService.CurrentVersion}");
    }

    private void AppendChat(string role, string message)
    {
        txtChat.AppendText($"{role}: {message}{Environment.NewLine}{Environment.NewLine}");
    }

    #endregion

    #region Settings & Update

    private void OnSaveAi(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtEndpoint.Text) ||
            string.IsNullOrWhiteSpace(txtModel.Text) ||
            string.IsNullOrWhiteSpace(txtApiKey.Password))
        {
            MessageBox.Show(this, Loc.T("MissingApi"), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _settings.ApiEndpoint = txtEndpoint.Text.Trim();
        _settings.Model = txtModel.Text.Trim();
        _settingsService.SetApiKey(_settings, txtApiKey.Password);
        _settingsService.Save(_settings);
        MessageBox.Show(this, Loc.T("SettingsSaved"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSaveProtection(object sender, RoutedEventArgs e)
    {
        var paths = txtProtectedPaths.Text
            .Split(new[] { Environment.NewLine, "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        _settings.ProtectedPaths = paths;
        _settingsService.Save(_settings);
        MessageBox.Show(this, Loc.T("SettingsSaved"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        btnCheckUpdate.IsEnabled = false;
        try
        {
            var info = await _update.CheckAsync(_settings);
            if (info is null)
            {
                MessageBox.Show(this, Loc.T("NoUpdate"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!Confirm(Loc.F("UpdateAvailable", info.Version))) return;
            var packagePath = await _update.DownloadAndVerifyAsync(info);
            _update.StartUpdater(packagePath);
            MessageBox.Show(this, Loc.T("UpdateStarted"), Loc.T("Done"), MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("UpdateFailed", ex.Message), Loc.T("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnCheckUpdate.IsEnabled = true;
        }
    }

    private void OnOpenRepo(object sender, RoutedEventArgs e)
    {
        var url = $"https://github.com/{_settings.GitHubOwner}/{_settings.GitHubRepo}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    #endregion

    private bool Confirm(string message) =>
        MessageBox.Show(this, message, Loc.T("ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}

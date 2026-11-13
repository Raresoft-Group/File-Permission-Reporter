using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FilePermissionReporter.Models;
using FilePermissionReporter.Services;
using Microsoft.Win32;

namespace FilePermissionReporter;

public partial class MainWindow : Window
{
    private readonly AuditScanner _scanner = new();
    private ScanResult? _result;
    private CancellationTokenSource? _scanCancellation;
    private DirectoryNode? _rootNode;
    private DirectoryRecord? _selectedDirectory;
    private string? _lastReportPath;
    private bool _english;

    public MainWindow()
    {
        InitializeComponent();
        var language = CultureInfo.CurrentUICulture.Name;
        _english = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        App.SetLanguage(_english);
        LanguageButton.Content = _english ? "中文" : "EN";
        ConcurrencySlider.Maximum = Math.Clamp(Environment.ProcessorCount * 4, 8, 64);
        ConcurrencySlider.Value = Math.Clamp(Environment.ProcessorCount * 2, 4, 32);
        ConcurrencyValueText.Text = $"Auto ({ConcurrencySlider.Value:0})";
        StatusText.Text = App.Text("Ready");
        UpdateLocalizedHeaders();
        UpdateSearchHints();
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _english = !_english;
        App.SetLanguage(_english);
        LanguageButton.Content = _english ? "中文" : "EN";
        UpdateLocalizedHeaders();
        RefreshShareNotice();
        UpdateLocalizedPermissionLabels();
        UpdateAdMemberCount();
        UpdatePermissionView();
        UpdateSearchHints();
        if (_scanCancellation is null)
            StatusText.Text = _result is null ? App.Text("Ready") : _result.WasCancelled ? App.Text("ScanCancelled") : _result.IsIncomplete ? App.Text("ScanIncomplete") : App.Text("ScanComplete");
    }

    private void UpdateLocalizedPermissionLabels()
    {
        if (_result is null)
            return;

        foreach (var permission in _result.Directories.SelectMany(directory => directory.Permissions))
        {
            permission.DisplayRights = RightsClassifier.Localize(permission.RightsCategory, permission.RightsRaw, _english);
            permission.DisplayAccessType = RightsClassifier.LocalizeAccessType(permission.AccessType, _english);
            permission.DisplayInheritance = RightsClassifier.LocalizeInheritance(permission.IsInherited, _english);
            var members = new Stack<AdMemberNode>(permission.GroupMembers);
            while (members.Count > 0)
            {
                var member = members.Pop();
                member.DisplayObjectType = RightsClassifier.LocalizeObjectType(member.ObjectType, _english);
                foreach (var child in member.Members)
                    members.Push(child);
            }
        }
        PermissionGrid.Items.Refresh();
    }

    private void UpdateLocalizedHeaders()
    {
        SetHeaders(PermissionGrid, "Account", "Rights", "Type", "Inherited");
        SetHeaders(ShareGrid, "Name", "SharePath", "Account", "ShareRight", "Type");
        SetHeaders(IssueGrid, "Stage", "Path", "Message");
    }

    private static void SetHeaders(DataGrid grid, params string[] resourceKeys)
    {
        for (var index = 0; index < Math.Min(grid.Columns.Count, resourceKeys.Length); index++)
            grid.Columns[index].Header = App.Text(resourceKeys[index]);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = App.Text("RootPath"),
            Multiselect = false
        };
        if (Directory.Exists(RootPathBox.Text))
            dialog.InitialDirectory = RootPathBox.Text;
        if (dialog.ShowDialog(this) == true)
            RootPathBox.Text = dialog.FolderName;
    }

    private void RootPathBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshShareNotice();

    private void ShareCheck_Changed(object sender, RoutedEventArgs e) => RefreshShareNotice();

    private void RefreshShareNotice()
    {
        if (ShareNoticeText is null || ShareCheck is null || RootPathBox is null)
            return;
        var isRemote = RootPathBox.Text.TrimStart().StartsWith("\\\\", StringComparison.Ordinal);
        ShareNoticeText.Text = isRemote ? App.Text("UncSharesNotice") : "";
        ShareNoticeText.Visibility = isRemote && ShareCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UnlimitedCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (DepthBox is not null)
            DepthBox.IsEnabled = UnlimitedCheck.IsChecked != true;
    }

    private void AutomaticConcurrencyCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (ConcurrencySlider is null)
            return;
        var automatic = AutomaticConcurrencyCheck.IsChecked == true;
        ConcurrencySlider.IsEnabled = !automatic;
        ConcurrencyValueText.Text = automatic ? $"Auto ({ConcurrencySlider.Value:0})" : ConcurrencySlider.Value.ToString("0", CultureInfo.InvariantCulture);
    }

    private void ConcurrencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ConcurrencyValueText is null || ConcurrencySlider is null)
            return;
        ConcurrencyValueText.Text = AutomaticConcurrencyCheck.IsChecked == true
            ? $"Auto ({ConcurrencySlider.Value:0})"
            : ConcurrencySlider.Value.ToString("0", CultureInfo.InvariantCulture);
    }

    private async void StartScanButton_Click(object sender, RoutedEventArgs e)
    {
        var root = RootPathBox.Text.Trim();
        if (!Directory.Exists(root))
        {
            MessageBox.Show(this, App.Text("RootError"), App.Text("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            RootPathBox.Focus();
            return;
        }

        var unlimited = UnlimitedCheck.IsChecked == true;
        var depth = 3;
        if (!unlimited && (!int.TryParse(DepthBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out depth) || depth < 1))
        {
            MessageBox.Show(this, App.Text("InputDepthError"), App.Text("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            DepthBox.Focus();
            return;
        }

        var options = new ScanOptions
        {
            RootPath = root,
            MaxDepth = depth,
            UnlimitedDepth = unlimited,
            IncludeSharePermissions = ShareCheck.IsChecked == true,
            ResolveAdGroups = AdCheck.IsChecked == true,
            MaxConcurrency = AutomaticConcurrencyCheck.IsChecked == true ? 0 : (int)Math.Round(ConcurrencySlider.Value)
        };

        _scanCancellation = new CancellationTokenSource();
        _result = null;
        _lastReportPath = null;
        AdMemberCountText.Visibility = Visibility.Collapsed;
        SaveReportButton.IsEnabled = false;
        OpenReportButton.IsEnabled = false;
        StartScanButton.IsEnabled = false;
        CancelScanButton.IsEnabled = true;
        ScanProgressBar.IsIndeterminate = true;
        ScanProgressBar.Value = 0;
        StatusText.Text = App.Text("Scanning");
        ShareGrid.ItemsSource = null;
        IssueGrid.ItemsSource = null;
        SetStats(null);
        PermissionGrid.ItemsSource = null;
        DirectoryTree.ItemsSource = null;
        _rootNode = null;
        _selectedDirectory = null;
        SelectedPathText.Text = App.Text("NoScan");
        SetScanControlsEnabled(false);

        try
        {
            var progress = new Progress<ScanProgress>(UpdateProgress);
            _result = await _scanner.ScanAsync(options, progress, _scanCancellation.Token);
            UpdateScanView();
            SaveReportButton.IsEnabled = _result.Directories.Count > 0;
            StatusText.Text = _result.WasCancelled
                ? App.Text("ScanCancelled")
                : _result.IsIncomplete
                    ? App.Text("ScanIncomplete")
                    : App.Text("ScanComplete");
            ScanProgressBar.Value = 100;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = App.Text("ScanCancelled");
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            MessageBox.Show(this, exception.Message, App.Text("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanProgressBar.IsIndeterminate = false;
            StartScanButton.IsEnabled = true;
            CancelScanButton.IsEnabled = false;
            SetScanControlsEnabled(true);
            _scanCancellation?.Dispose();
            _scanCancellation = null;
        }
    }

    private void SetScanControlsEnabled(bool enabled)
    {
        RootPathBox.IsEnabled = enabled;
        BrowseButton.IsEnabled = enabled;
        UnlimitedCheck.IsEnabled = enabled;
        DepthBox.IsEnabled = enabled && UnlimitedCheck.IsChecked != true;
        ShareCheck.IsEnabled = enabled;
        AdCheck.IsEnabled = enabled;
        AutomaticConcurrencyCheck.IsEnabled = enabled;
        ConcurrencySlider.IsEnabled = enabled && AutomaticConcurrencyCheck.IsChecked != true;
    }

    private void UpdateProgress(ScanProgress progress)
    {
        var prefix = App.Text("Scanning");
        StatusText.Text = $"{prefix}: {progress.ProcessedDirectories:N0} · {progress.CurrentPath}";
        StatusText.ToolTip = progress.Message;
    }

    private void CancelScanButton_Click(object sender, RoutedEventArgs e) => _scanCancellation?.Cancel();

    private void UpdateScanView()
    {
        if (_result is null)
            return;

        UpdateLocalizedPermissionLabels();
        SetStats(_result);
        ShareGrid.ItemsSource = _result.SharePermissions;
        IssueGrid.ItemsSource = _result.Issues;
        BuildDirectoryTree(_result);
        UpdateAdMemberCount();
        RefreshShareNotice();

        if (ShareCheck.IsChecked == true && RootPathBox.Text.TrimStart().StartsWith("\\\\", StringComparison.Ordinal))
            StatusText.ToolTip = App.Text("UncSharesNotice");
        else
            StatusText.ToolTip = $"{_result.Duration.TotalSeconds:N1} s · {_result.SkippedReparsePoints:N0} skipped reparse points";
    }

    private void UpdateAdMemberCount()
    {
        if (AdMemberCountText is null)
            return;
        if (_result is null || AdCheck.IsChecked != true)
        {
            AdMemberCountText.Visibility = Visibility.Collapsed;
            return;
        }
        AdMemberCountText.Text = $"{App.Text("AdMembers")}: {_result.ResolvedAdMemberCount:N0}";
        AdMemberCountText.Visibility = Visibility.Visible;
    }

    private void SetStats(ScanResult? result)
    {
        if (result is null)
        {
            StatStartedAt.Text = StatRoot.Text = StatDepth.Text = StatDirectories.Text = "—";
            StatRules.Text = StatPrincipals.Text = StatExplicit.Text = StatInherited.Text = "—";
            return;
        }

        StatStartedAt.Text = result.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        StatRoot.Text = result.RootPath;
        StatDepth.Text = result.UnlimitedDepth ? "∞" : result.RequestedDepth.ToString(CultureInfo.InvariantCulture);
        StatDirectories.Text = result.DirectoryCount.ToString("N0", CultureInfo.CurrentCulture);
        StatRules.Text = result.PermissionCount.ToString("N0", CultureInfo.CurrentCulture);
        StatPrincipals.Text = result.UniquePrincipalCount.ToString("N0", CultureInfo.CurrentCulture);
        StatExplicit.Text = result.ExplicitPermissionCount.ToString("N0", CultureInfo.CurrentCulture);
        StatInherited.Text = result.InheritedPermissionCount.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void BuildDirectoryTree(ScanResult result)
    {
        var nodes = result.Directories.ToDictionary(record => record.Path, record => new DirectoryNode(record), StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes.Values)
        {
            var parentPath = node.Record.ParentPath;
            if (parentPath.Length > 0 && nodes.TryGetValue(parentPath, out var parent))
                parent.Children.Add(node);
        }

        _rootNode = nodes.Values.FirstOrDefault(node => node.Record.ParentPath.Length == 0);
        if (_rootNode is null)
        {
            DirectoryTree.ItemsSource = null;
            return;
        }

        _rootNode.IsExpanded = true;
        DirectoryTree.ItemsSource = new ObservableCollection<DirectoryNode> { _rootNode };
        SelectDirectory(_rootNode.Record);
    }

    private void DirectoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is DirectoryNode node)
            SelectDirectory(node.Record);
    }

    private void SelectDirectory(DirectoryRecord record)
    {
        _selectedDirectory = record;
        SelectedPathText.Text = record.Path + (record.AclRead ? "" : $" · {record.ErrorMessage}");
        SelectedPathText.ToolTip = record.Path;
        UpdatePermissionView();
    }

    private void UpdatePermissionView()
    {
        if (_selectedDirectory is null)
        {
            PermissionGrid.ItemsSource = null;
            PermissionEmptyText.Visibility = Visibility.Visible;
            return;
        }

        var query = PermissionSearchBox.Text.Trim();
        var permissions = _selectedDirectory.Permissions.AsEnumerable();
        if (query.Length > 0)
        {
            permissions = permissions.Where(permission =>
                permission.Identity.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                permission.RightsRaw.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                permission.RightsMask.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                permission.DisplayRights.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                permission.AccessType.Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        var filtered = permissions.ToList();
        PermissionGrid.ItemsSource = filtered;
        PermissionEmptyText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        GroupMembersTree.Visibility = Visibility.Collapsed;
        GroupMembersTitle.Visibility = Visibility.Collapsed;
        GroupMembersTree.ItemsSource = null;
    }

    private void PermissionSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchHints();
        UpdatePermissionView();
    }

    private void UpdateSearchHints()
    {
        if (DirectorySearchHint is not null)
            DirectorySearchHint.Visibility = string.IsNullOrEmpty(DirectorySearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        if (PermissionSearchHint is not null)
            PermissionSearchHint.Visibility = string.IsNullOrEmpty(PermissionSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PermissionGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PermissionGrid.SelectedItem is PermissionEntry entry && entry.IsAdGroup)
        {
            GroupMembersTree.ItemsSource = entry.GroupMembers;
            GroupMembersTree.Visibility = Visibility.Visible;
            GroupMembersTitle.Visibility = Visibility.Visible;
        }
        else
        {
            GroupMembersTree.ItemsSource = null;
            GroupMembersTree.Visibility = Visibility.Collapsed;
            GroupMembersTitle.Visibility = Visibility.Collapsed;
        }
    }

    private void DirectorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchHints();
        if (_rootNode is null)
            return;

        var query = DirectorySearchBox.Text.Trim();
        var nodes = FlattenNodes(_rootNode);
        if (query.Length == 0)
        {
            foreach (var node in nodes)
                node.IsVisible = true;
            return;
        }

        foreach (var node in nodes)
            node.IsVisible = (node.Record.Name + " " + node.Record.Path).Contains(query, StringComparison.OrdinalIgnoreCase);
        foreach (var node in nodes.OrderByDescending(node => node.Record.Level))
        {
            var childMatch = node.Children.Any(child => child.IsVisible);
            node.IsVisible = node.IsVisible || childMatch;
            if (childMatch)
                node.IsExpanded = true;
        }
    }

    private static List<DirectoryNode> FlattenNodes(DirectoryNode root)
    {
        var nodes = new List<DirectoryNode>();
        var pending = new Stack<DirectoryNode>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            nodes.Add(node);
            foreach (var child in node.Children)
                pending.Push(child);
        }
        return nodes;
    }

    private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rootNode is null)
            return;
        foreach (var node in FlattenNodes(_rootNode))
            node.IsExpanded = true;
    }

    private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rootNode is null)
            return;
        foreach (var node in FlattenNodes(_rootNode))
            node.IsExpanded = false;
    }

    private async void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null || _result.Directories.Count == 0)
            return;

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var dialog = new SaveFileDialog
        {
            Title = App.Text("SaveReport"),
            FileName = "report.html",
            DefaultExt = ".html",
            Filter = "HTML report (*.html)|*.html|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = Directory.Exists(documents) ? documents : Environment.CurrentDirectory
        };
        if (dialog.ShowDialog(this) != true)
            return;

        SaveReportButton.IsEnabled = false;
        try
        {
            await ReportGenerator.SaveAsync(_result, dialog.FileName, _english);
            _lastReportPath = dialog.FileName;
            OpenReportButton.IsEnabled = true;
            StatusText.Text = App.Text("Saved");
            StatusText.ToolTip = dialog.FileName;
            if (_result.IsIncomplete)
                MessageBox.Show(this, App.Text("ScanIncomplete"), App.Text("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, App.Text("SaveReport"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveReportButton.IsEnabled = _result is not null && _result.Directories.Count > 0;
        }
    }

    private void OpenReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastReportPath) || !File.Exists(_lastReportPath))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(_lastReportPath) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, App.Text("OpenReport"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

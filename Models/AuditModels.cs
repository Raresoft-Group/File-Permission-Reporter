using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FilePermissionReporter.Models;

public sealed class ScanOptions
{
    public required string RootPath { get; init; }
    public int MaxDepth { get; init; } = 3;
    public bool UnlimitedDepth { get; init; }
    public bool IncludeSharePermissions { get; init; }
    public bool ResolveAdGroups { get; init; }
    public int MaxConcurrency { get; init; }
}

public sealed class PermissionEntry
{
    public string Identity { get; init; } = "";
    public string Sid { get; init; } = "";
    public string RightsRaw { get; init; } = "";
    public int RightsMask { get; init; }
    public string FriendlyRights { get; init; } = "";
    public string DisplayRights { get; set; } = "";
    public string DisplayAccessType { get; set; } = "";
    public string DisplayInheritance { get; set; } = "";
    public string RightsCategory { get; init; } = "custom";
    public string AccessType { get; init; } = "Allow";
    public bool IsInherited { get; init; }
    public string InheritanceFlags { get; init; } = "None";
    public string PropagationFlags { get; init; } = "None";
    public bool IsAdGroup { get; set; }
    public string DirectoryPath { get; set; } = "";
    public bool GroupResolved { get; set; }
    public List<AdMemberNode> GroupMembers { get; set; } = [];
}

public sealed class AdMemberNode
{
    public string Account { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ObjectType { get; init; } = "";
    public string DisplayObjectType { get; set; } = "";
    public List<AdMemberNode> Members { get; init; } = [];
}

public sealed class DirectoryRecord
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string ParentPath { get; init; } = "";
    public int Level { get; init; }
    public bool AclRead { get; set; }
    public string ErrorMessage { get; set; } = "";
    public List<PermissionEntry> Permissions { get; init; } = [];
}

public sealed record ScanIssue(string Path, string Stage, string Message);

public sealed record ShareAccess(string ShareName, string SharePath, string Account, string AccessRight, string AccessType);

public sealed class ScanProgress
{
    public int ProcessedDirectories { get; init; }
    public string CurrentPath { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class ScanResult
{
    public string RootPath { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public TimeSpan Duration { get; set; }
    public int RequestedDepth { get; init; }
    public bool UnlimitedDepth { get; init; }
    public bool WasCancelled { get; set; }
    public bool IsIncomplete { get; set; }
    public int SkippedReparsePoints { get; set; }
    public int ResolvedAdMemberCount { get; set; }
    public string ShareStatusMessage { get; set; } = "";
    public List<DirectoryRecord> Directories { get; init; } = [];
    public List<ShareAccess> SharePermissions { get; init; } = [];
    public List<ScanIssue> Issues { get; init; } = [];

    public int DirectoryCount => Directories.Count;
    public int PermissionCount => Directories.Sum(directory => directory.Permissions.Count);
    public int UniquePrincipalCount => Directories
        .SelectMany(directory => directory.Permissions)
        .Select(permission => permission.Identity)
        .Where(identity => !string.IsNullOrWhiteSpace(identity))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public int ExplicitPermissionCount => Directories.SelectMany(directory => directory.Permissions).Count(permission => !permission.IsInherited);
    public int InheritedPermissionCount => Directories.SelectMany(directory => directory.Permissions).Count(permission => permission.IsInherited);
    public string Status => WasCancelled ? "Cancelled" : IsIncomplete ? "Incomplete" : "Complete";
}

public sealed class DirectoryNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isVisible = true;

    public DirectoryNode(DirectoryRecord record) => Record = record;

    public DirectoryRecord Record { get; }
    public ObservableCollection<DirectoryNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { if (_isVisible == value) return; _isVisible = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

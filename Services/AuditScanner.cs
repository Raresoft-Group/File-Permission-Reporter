using System.Collections.Concurrent;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using FilePermissionReporter.Models;

namespace FilePermissionReporter.Services;

public sealed class AuditScanner
{
    public async Task<ScanResult> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new ScanResult
        {
            RootPath = Path.GetFullPath(options.RootPath),
            StartedAt = startedAt,
            RequestedDepth = options.MaxDepth,
            UnlimitedDepth = options.UnlimitedDepth
        };

        var records = new ConcurrentBag<DirectoryRecord>();
        var issues = new ConcurrentBag<ScanIssue>();
        var skippedReparsePoints = 0;
        var processed = 0;
        var concurrency = options.MaxConcurrency > 0
            ? Math.Clamp(options.MaxConcurrency, 1, 64)
            : Math.Clamp(Environment.ProcessorCount * 2, 4, 32);
        var sidNames = new ConcurrentDictionary<string, Lazy<string>>(StringComparer.OrdinalIgnoreCase);

        var currentLevel = new List<ScanItem> { new(result.RootPath, "", 1) };
        var wasCancelled = false;
        var depthLimit = options.UnlimitedDepth ? int.MaxValue : Math.Max(1, options.MaxDepth);

        while (currentLevel.Count > 0)
        {
            var nextLevel = new ConcurrentBag<ScanItem>();
            try
            {
                await Parallel.ForEachAsync(
                    currentLevel,
                    new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = cancellationToken },
                    (item, token) =>
                    {
                        token.ThrowIfCancellationRequested();
                        var record = new DirectoryRecord
                        {
                            Id = item.Path,
                            Path = item.Path,
                            Name = item.Level == 1 ? item.Path : System.IO.Path.GetFileName(item.Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)),
                            ParentPath = item.ParentPath,
                            Level = item.Level
                        };

                        try
                        {
                            var directory = new DirectoryInfo(item.Path);
                            var security = directory.GetAccessControl(AccessControlSections.Access);
                            var accessRules = security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier));
                            foreach (FileSystemAccessRule rule in accessRules)
                            {
                                var sid = rule.IdentityReference as SecurityIdentifier;
                                var sidValue = sid?.Value ?? rule.IdentityReference.Value;
                                var account = sid is null ? sidValue : ResolveAccountName(sid, sidNames);
                                var (friendly, category) = RightsClassifier.Classify(rule.FileSystemRights);
                                record.Permissions.Add(new PermissionEntry
                                {
                                    Identity = account,
                                    Sid = sidValue,
                                    RightsRaw = rule.FileSystemRights.ToString(),
                                    RightsMask = (int)rule.FileSystemRights,
                                    FriendlyRights = friendly,
                                    RightsCategory = category,
                                    AccessType = rule.AccessControlType.ToString(),
                                    IsInherited = rule.IsInherited,
                                    InheritanceFlags = rule.InheritanceFlags.ToString(),
                                    PropagationFlags = rule.PropagationFlags.ToString()
                                });
                            }
                            record.AclRead = true;
                        }
                        catch (Exception exception)
                        {
                            record.ErrorMessage = exception.Message;
                            issues.Add(new ScanIssue(item.Path, "NTFS", exception.Message));
                        }

                        records.Add(record);

                        var isReparseRoot = false;
                        if (item.Level == 1)
                        {
                            try
                            {
                                isReparseRoot = (File.GetAttributes(item.Path) & FileAttributes.ReparsePoint) != 0;
                                if (isReparseRoot)
                                    Interlocked.Increment(ref skippedReparsePoints);
                            }
                            catch (Exception exception)
                            {
                                issues.Add(new ScanIssue(item.Path, "Enumeration", exception.Message));
                            }
                        }

                        if (item.Level < depthLimit && !isReparseRoot)
                        {
                            try
                            {
                                foreach (var childPath in Directory.EnumerateDirectories(item.Path))
                                {
                                    token.ThrowIfCancellationRequested();
                                    try
                                    {
                                        if ((File.GetAttributes(childPath) & FileAttributes.ReparsePoint) != 0)
                                        {
                                            Interlocked.Increment(ref skippedReparsePoints);
                                            continue;
                                        }
                                        nextLevel.Add(new ScanItem(childPath, item.Path, item.Level + 1));
                                    }
                                    catch (Exception exception)
                                    {
                                        issues.Add(new ScanIssue(childPath, "Enumeration", exception.Message));
                                    }
                                }
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception exception)
                            {
                                issues.Add(new ScanIssue(item.Path, "Enumeration", exception.Message));
                            }
                        }

                        var completed = Interlocked.Increment(ref processed);
                        progress?.Report(new ScanProgress
                        {
                            ProcessedDirectories = completed,
                            CurrentPath = item.Path,
                            Message = $"Scanned {completed:N0} directories"
                        });
                        return ValueTask.CompletedTask;
                    });
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                break;
            }

            currentLevel = nextLevel.OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        result.Directories.AddRange(records.OrderBy(record => record.Level).ThenBy(record => record.Path, StringComparer.OrdinalIgnoreCase));
        result.SkippedReparsePoints = skippedReparsePoints;
        result.WasCancelled = wasCancelled || cancellationToken.IsCancellationRequested;
        result.IsIncomplete = issues.Count > 0 || result.WasCancelled;

        if (options.IncludeSharePermissions)
        {
            if (IsUncPath(result.RootPath))
            {
                result.ShareStatusMessage = "Remote SMB share permissions were not queried. Only local shares are supported.";
            }
            else if (!result.WasCancelled)
            {
                try
                {
                    var shareResult = await SharePermissionReader.ReadForLocalPathAsync(result.RootPath, cancellationToken);
                    result.SharePermissions.AddRange(shareResult.Access);
                    foreach (var issue in shareResult.Issues)
                        issues.Add(issue);
                    result.ShareStatusMessage = shareResult.Issues.Count == 0
                        ? "Local SMB share permissions collected."
                        : "Some local SMB share permissions could not be read.";
                }
                catch (OperationCanceledException)
                {
                    result.WasCancelled = true;
                    result.IsIncomplete = true;
                }
                catch (Exception exception)
                {
                    issues.Add(new ScanIssue(result.RootPath, "SMB", exception.Message));
                    result.ShareStatusMessage = exception.Message;
                }
            }
        }

        result.Issues.AddRange(issues.OrderBy(issue => issue.Path, StringComparer.OrdinalIgnoreCase));
        if (options.ResolveAdGroups && !result.WasCancelled)
        {
            var adProgress = new Progress<ScanProgress>(item => progress?.Report(item));
            try
            {
                await AdGroupResolver.ResolveAsync(result, adProgress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                result.WasCancelled = true;
                result.IsIncomplete = true;
            }
        }

        result.IsIncomplete = result.IsIncomplete || result.Issues.Count > 0 || result.WasCancelled;
        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private static string ResolveAccountName(SecurityIdentifier sid, ConcurrentDictionary<string, Lazy<string>> cache)
    {
        return cache.GetOrAdd(sid.Value, _ => new Lazy<string>(() =>
        {
            try { return sid.Translate(typeof(NTAccount)).Value; }
            catch { return sid.Value; }
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static bool IsUncPath(string path) => path.StartsWith("\\\\", StringComparison.Ordinal);
    private sealed record ScanItem(string Path, string ParentPath, int Level);
}

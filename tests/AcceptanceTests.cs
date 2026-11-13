using System.IO;
using System.Security.AccessControl;
using FilePermissionReporter.Models;
using FilePermissionReporter.Services;

namespace FilePermissionReporter.Tests;

public sealed class AcceptanceTests
{
    [Fact]
    public void RightsClassifierLabelsCommonRulesAndPreservesUnknownMasks()
    {
        Assert.Equal(("Full Control", "full"), RightsClassifier.Classify(FileSystemRights.FullControl));
        Assert.Equal(("Modify", "modify"), RightsClassifier.Classify(FileSystemRights.Modify));
        Assert.Equal(("Read", "read"), RightsClassifier.Classify(FileSystemRights.Read));

        var custom = RightsClassifier.Classify(FileSystemRights.WriteData | FileSystemRights.Delete);
        Assert.Equal("custom", custom.Category);
        Assert.Contains("CreateFiles", custom.Label, StringComparison.Ordinal);
        Assert.Contains("Delete", custom.Label, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanDepthCountsRootAsLevelOne()
    {
        var root = CreateTree();
        try
        {
            var scanner = new AuditScanner();
            var result = await scanner.ScanAsync(new ScanOptions
            {
                RootPath = root,
                MaxDepth = 2,
                MaxConcurrency = 2
            }, progress: null, CancellationToken.None);

            Assert.Equal(3, result.DirectoryCount);
            Assert.Contains(result.Directories, directory => directory.Path.Equals(root, StringComparison.OrdinalIgnoreCase) && directory.Level == 1);
            Assert.All(result.Directories, directory => Assert.True(directory.Level <= 2));
            Assert.Equal(result.PermissionCount, result.ExplicitPermissionCount + result.InheritedPermissionCount);
            Assert.True(result.Directories.Single(directory => directory.Path.Equals(root, StringComparison.OrdinalIgnoreCase)).AclRead);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnlimitedScanIncludesEveryNestedDirectory()
    {
        var root = CreateTree();
        try
        {
            var result = await new AuditScanner().ScanAsync(new ScanOptions
            {
                RootPath = root,
                UnlimitedDepth = true,
                MaxDepth = 3,
                MaxConcurrency = 3
            }, progress: null, CancellationToken.None);

            Assert.Equal(4, result.DirectoryCount);
            Assert.Contains(result.Directories, directory => directory.Level == 3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanHandlesTenThousandChildDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fpr-large-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            for (var index = 0; index < 10_000; index++)
                Directory.CreateDirectory(Path.Combine(root, $"D{index:D5}"));

            var result = await new AuditScanner().ScanAsync(new ScanOptions
            {
                RootPath = root,
                MaxDepth = 2,
                MaxConcurrency = 16
            }, progress: null, CancellationToken.None);

            Assert.Equal(10_001, result.DirectoryCount);
            Assert.Equal(10_001, result.Directories.Select(directory => directory.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HtmlReportIsStandaloneAndEncodesUntrustedNames()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"fpr-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "report.html");
        try
        {
            var result = new ScanResult
            {
                RootPath = @"C:\Shares\<script>alert(1)</script>",
                StartedAt = DateTimeOffset.Now,
                RequestedDepth = 1,
                Directories =
                [
                    new DirectoryRecord
                    {
                        Id = "root",
                        Name = "<script>alert(1)</script>",
                        Path = @"C:\Shares\<script>alert(1)</script>",
                        Level = 1,
                        AclRead = true,
                        Permissions =
                        [
                            new PermissionEntry
                            {
                                Identity = "DOMAIN\\<img src=x onerror=alert(2)>",
                                Sid = "S-1-5-21-1-2-3-1001",
                                RightsRaw = "ReadAndExecute",
                                RightsMask = (int)FileSystemRights.ReadAndExecute,
                                FriendlyRights = "Read",
                                RightsCategory = "read",
                                AccessType = "Allow",
                                IsInherited = true,
                                InheritanceFlags = "ContainerInherit",
                                PropagationFlags = "None"
                            }
                        ]
                    }
                ]
            };

            await ReportGenerator.SaveAsync(result, outputPath, english: false);
            var html = await File.ReadAllTextAsync(outputPath);

            Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("const DATA=", html, StringComparison.Ordinal);
            Assert.Contains("rightsRaw", html, StringComparison.Ordinal);
            Assert.Contains("inheritanceFlags", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<img src=x onerror=alert(2)>", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script src=", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string CreateTree()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fpr-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Finance", "AP"));
        Directory.CreateDirectory(Path.Combine(root, "HR"));
        return root;
    }
}

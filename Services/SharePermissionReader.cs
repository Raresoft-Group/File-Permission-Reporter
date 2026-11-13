using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FilePermissionReporter.Models;

namespace FilePermissionReporter.Services;

public static class SharePermissionReader
{
    public static async Task<(IReadOnlyList<ShareAccess> Access, IReadOnlyList<ScanIssue> Issues)> ReadForLocalPathAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(rootPath));
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $root = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedPath}}'))
            $root = [IO.Path]::GetFullPath($root).TrimEnd('\')
            $access = [System.Collections.Generic.List[object]]::new()
            $issues = [System.Collections.Generic.List[object]]::new()
            try {
                $shares = @(Get-SmbShare | Where-Object {
                    $sharePath = [IO.Path]::GetFullPath($_.Path).TrimEnd('\')
                    $root.Equals($sharePath, [StringComparison]::OrdinalIgnoreCase) -or
                    $root.StartsWith($sharePath + '\', [StringComparison]::OrdinalIgnoreCase) -or
                    $sharePath.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
                })
                foreach ($share in $shares) {
                    try {
                        foreach ($rule in (Get-SmbShareAccess -Name $share.Name)) {
                            $access.Add([pscustomobject]@{
                                ShareName = [string]$share.Name
                                SharePath = [string]$share.Path
                                Account = [string]$rule.AccountName
                                AccessRight = [string]$rule.AccessRight
                                AccessType = [string]$rule.AccessControlType
                            })
                        }
                    } catch {
                        $issues.Add([pscustomobject]@{ Path = [string]$share.Path; Stage = 'SMB'; Message = $_.Exception.Message })
                    }
                }
            } catch {
                $issues.Add([pscustomobject]@{ Path = $root; Stage = 'SMB'; Message = $_.Exception.Message })
            }
            [pscustomobject]@{ Access = @($access); Issues = @($issues) } | ConvertTo-Json -Depth 5 -Compress
            """;

        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedScript);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Could not start Windows PowerShell.");

        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* The process may have exited between the check and kill. */ }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "SMB share query failed." : stderr.Trim());
        if (string.IsNullOrWhiteSpace(stdout))
            return ([], string.IsNullOrWhiteSpace(stderr) ? [] : [new ScanIssue(rootPath, "SMB", stderr.Trim())]);

        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        var access = ReadArray(root, "Access")
            .Select(item => new ShareAccess(
                GetString(item, "ShareName"),
                GetString(item, "SharePath"),
                GetString(item, "Account"),
                GetString(item, "AccessRight"),
                GetString(item, "AccessType")))
            .ToList();
        var issues = ReadArray(root, "Issues")
            .Select(item => new ScanIssue(GetString(item, "Path"), GetString(item, "Stage"), GetString(item, "Message")))
            .ToList();
        return (access, issues);
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return [];
        return property.ValueKind == JsonValueKind.Array ? property.EnumerateArray().ToArray() : [property];
    }

    private static string GetString(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }
}

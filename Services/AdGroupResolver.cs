using System.Security.Principal;
using FilePermissionReporter.Models;
using System.DirectoryServices.AccountManagement;

namespace FilePermissionReporter.Services;

public static class AdGroupResolver
{
    public static Task ResolveAsync(ScanResult result, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() => Resolve(result, progress, cancellationToken), cancellationToken);
    }

    private static void Resolve(ScanResult result, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var groups = result.Directories
            .SelectMany(directory => directory.Permissions)
            .Where(permission => !string.IsNullOrWhiteSpace(permission.Identity))
            .GroupBy(permission => permission.Sid.Length > 0 ? permission.Sid : permission.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var cache = new Dictionary<string, GroupExpansion?>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<ScanIssue>();
        var resolvedMembers = 0;
        var processed = 0;

        foreach (var permission in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = permission.Sid.Length > 0 ? permission.Sid : permission.Identity;
            try
            {
                if (!cache.TryGetValue(key, out var expansion))
                {
                    expansion = ResolveGroup(permission);
                    cache[key] = expansion;
                }

                if (expansion is not null)
                {
                    foreach (var entry in result.Directories.SelectMany(directory => directory.Permissions)
                                 .Where(entry => string.Equals(entry.Sid.Length > 0 ? entry.Sid : entry.Identity, key, StringComparison.OrdinalIgnoreCase)))
                    {
                        entry.IsAdGroup = true;
                        entry.GroupResolved = true;
                        entry.GroupMembers = expansion.Members;
                    }
                    resolvedMembers += expansion.FlatMemberCount;
                }
            }
            catch (Exception exception)
            {
                foreach (var entry in result.Directories.SelectMany(directory => directory.Permissions)
                             .Where(entry => string.Equals(entry.Sid.Length > 0 ? entry.Sid : entry.Identity, key, StringComparison.OrdinalIgnoreCase)))
                    entry.IsAdGroup = true;
                failures.Add(new ScanIssue(permission.Identity, "AD", exception.Message));
            }

            processed++;
            progress?.Report(new ScanProgress
            {
                ProcessedDirectories = processed,
                CurrentPath = permission.Identity,
                Message = $"Resolved {processed:N0} principals"
            });
        }

        result.ResolvedAdMemberCount = resolvedMembers;
        result.Issues.AddRange(failures);
        result.IsIncomplete = result.IsIncomplete || failures.Count > 0;
    }

    private static GroupExpansion? ResolveGroup(PermissionEntry entry)
    {
        var identityName = entry.Identity;
        if (identityName.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase) ||
            identityName.StartsWith("BUILTIN\\", StringComparison.OrdinalIgnoreCase) ||
            identityName.Equals("Everyone", StringComparison.OrdinalIgnoreCase) ||
            identityName.Equals("NT AUTHORITY\\Authenticated Users", StringComparison.OrdinalIgnoreCase))
            return null;

        var samName = identityName.Contains('\\') ? identityName[(identityName.LastIndexOf('\\') + 1)..] : identityName;
        if (string.IsNullOrWhiteSpace(samName) || samName.EndsWith('$'))
            return null;

        var domain = identityName.Contains('\\') ? identityName[..identityName.LastIndexOf('\\')] : null;
        if (domain is not null && (domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || domain.Equals("NT AUTHORITY", StringComparison.OrdinalIgnoreCase)))
            return null;

        PrincipalContext? context = null;
        Principal? principal = null;
        GroupPrincipal? group = null;
        try
        {
            context = domain is null
                ? new PrincipalContext(ContextType.Domain)
                : new PrincipalContext(ContextType.Domain, domain);

            if (entry.Sid.Length > 0)
            {
                try { principal = Principal.FindByIdentity(context, IdentityType.Sid, entry.Sid); }
                catch { /* Fall back to the domain-qualified account name. */ }
            }

            principal ??= Principal.FindByIdentity(context, IdentityType.SamAccountName, samName);
            if (principal is not GroupPrincipal foundGroup)
            {
                var principalWasNotFound = principal is null;
                principal?.Dispose();
                principal = null;
                if (principalWasNotFound && domain is not null && !domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"无法在当前登录上下文中解析 AD 主体“{identityName}”。 / The AD principal '{identityName}' could not be resolved in the current logon context.");
                return null;
            }
            group = foundGroup;

            var rootKey = group.Sid?.Value ?? group.DistinguishedName ?? identityName;
            var members = ExpandMembers(group, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootKey }, out var count);
            return new GroupExpansion(members, count);
        }
        finally
        {
            if (group is not null)
                group.Dispose();
            else
                principal?.Dispose();
            context?.Dispose();
        }
    }

    private static List<AdMemberNode> ExpandMembers(GroupPrincipal group, HashSet<string> ancestors, out int memberCount)
    {
        memberCount = 0;
        var members = new List<AdMemberNode>();
        using var directMembers = group.GetMembers();
        foreach (var principal in directMembers)
        {
            using (principal)
            {
                var sid = principal.Sid?.Value ?? principal.DistinguishedName ?? principal.SamAccountName ?? principal.Name ?? "unknown";
                var account = string.IsNullOrWhiteSpace(principal.SamAccountName) ? principal.Name ?? sid : principal.SamAccountName;
                var node = new AdMemberNode
                {
                    Account = account,
                    DisplayName = string.IsNullOrWhiteSpace(principal.DisplayName) ? account : principal.DisplayName,
                    ObjectType = principal switch
                    {
                        GroupPrincipal => "Group",
                        UserPrincipal => "User",
                        ComputerPrincipal => "Computer",
                        _ => "Principal"
                    }
                };

                if (principal is GroupPrincipal childGroup)
                {
                    if (!ancestors.Contains(sid))
                    {
                        var nextAncestors = new HashSet<string>(ancestors, StringComparer.OrdinalIgnoreCase) { sid };
                        node.Members.AddRange(ExpandMembers(childGroup, nextAncestors, out var nestedCount));
                        memberCount += nestedCount;
                    }
                }

                members.Add(node);
                memberCount++;
            }
        }
        return members;
    }

    private sealed record GroupExpansion(List<AdMemberNode> Members, int FlatMemberCount);
}

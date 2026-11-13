# File Permission Reporter

Windows desktop utility for reviewing NTFS directory ACLs and creating a self-contained, offline HTML report.

## License

This project is licensed under the GNU General Public License v3.0 only (`GPL-3.0-only`). See [LICENSE](LICENSE). Commercial use and sale are permitted under the license terms. When distributing modified versions, comply with GPLv3 and provide the corresponding source as required by the license.

## Run from source

Requirements: Windows with Desktop Experience, .NET 10 SDK, and an Active Directory logon only if AD group expansion is enabled.

```powershell
dotnet run
```

Run the Windows acceptance tests with:

```powershell
dotnet test .\tests\FilePermissionReporter.Tests.csproj
```

## Create the portable package

```powershell
./publish-portable.ps1
```

The script creates `artifacts/FilePermissionReporter-win-x64.zip`. The package is self-contained and does not require a .NET Desktop Runtime installation. Run `FilePermissionReporter.exe` from the extracted folder.

## Scan behavior

- The root directory is depth 1. The default scan depth is 3; enable **Unlimited depth** for the full tree.
- NTFS rules are read using the current Windows logon. Denied paths are recorded and the scan continues.
- Junctions and symbolic-link directories are skipped. A selected root that is itself a reparse point is read for its ACL but is not traversed.
- AD group expansion is optional and uses the current Windows logon. Nested groups are expanded with cycle detection; raw ACL identities remain in the report when lookup fails.
- Optional SMB collection queries local shares that overlap the selected directory. Remote share permissions are not queried for UNC paths.
- A cancelled or partially failed scan can still produce a report marked incomplete.
- The HTML report embeds its CSS, JavaScript and collected data. It does not request network resources. Reports contain account names, directory paths and permissions; store them with appropriate file-system access controls.

The WPF app runs on Windows client workstations or Windows Server with Desktop Experience. Server Core is not supported as a local GUI host. Share collection uses the Windows PowerShell SMB cmdlets when available.

## Developer and maintenance notes

- [Portable build details](docs/BUILD_EXE.md)
- [Architecture and data flow](docs/ARCHITECTURE.md)
- [Maintenance experience and feature checklist](docs/MAINTENANCE.md)
- [Test coverage and validation limits](docs/VALIDATION.md)

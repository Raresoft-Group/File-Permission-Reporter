<p align="center">
  <img src="Assets/FilePermissionReporter.png" alt="File Permission Reporter icon" width="112">
</p>

<h1 align="center">File Permission Reporter</h1>

<p align="center">
  Review Windows folder permissions and create a self-contained, offline HTML report.
</p>

<p align="center"><strong>Version 1.0.0 · .NET 10 · WPF · Windows x64 · English and 简体中文</strong></p>

<p align="center"><a href="README-CN.md">简体中文</a></p>

File Permission Reporter scans NTFS access control entries (ACEs) and presents the rules found on each directory. It keeps individual rules visible so administrators can review what is configured without confusing that information with a user's effective access.

## Features

- Scan a folder tree with a default depth of three levels, or choose unlimited depth. The selected root is level one.
- Review Allow and Deny rules, explicit and inherited entries, original rights, masks, identities, and SIDs.
- Optionally resolve and expand Active Directory groups using the current Windows logon, including nested groups with cycle protection.
- Optionally collect permissions for matching shares on the local computer. UNC scans do not query remote SMB permissions.
- Search and browse results in the WPF app, then save them to one offline HTML file with embedded styles, scripts, and scan data.
- Use the app and report in English or Simplified Chinese.

## Important scope

The report shows the ACL rules collected from each directory. It does **not** calculate effective access, combine Allow and Deny rules, or determine whether a particular user can open a file.

AD and SMB collection are optional. AD lookup uses the current Windows identity; the app does not request or store separate credentials. SMB collection is limited to local shares. A UNC path can still be scanned for NTFS rules, but its remote share permissions are not queried.

## Get started

### Run from source

Requirements: Windows with Desktop Experience and the .NET 10 SDK. An Active Directory logon is needed only when AD group expansion is used.

```powershell
dotnet run
```

In the app, choose a folder, set the scan depth and optional collection settings, start the scan, then save the report when it finishes.

### Build the portable package

From the project root on Windows, run:

```powershell
.\publish-portable.ps1
```

The script creates `artifacts/FilePermissionReporter-win-x64.zip`. Extract the ZIP and run `FilePermissionReporter.exe`; the package is self-contained and does not require a separate .NET Desktop Runtime. The script clears and recreates the publish directory, then replaces the ZIP after a successful publish. See the [1.0.0 release notes](docs/RELEASE_NOTES_1.0.0.md) for the package summary.

### Run the acceptance tests

On Windows with the .NET 10 SDK:

```powershell
dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release
```

## Requirements and limitations

- Windows x64 with Desktop Experience. Windows Server Core is not supported as a GUI host.
- SMB collection requires the Windows SMB PowerShell cmdlets to be available.
- Real Active Directory and SMB environments, failure and cancellation scenarios, large-report interactions, and the Windows Server version matrix have not all received end-to-end validation.

## Reports and privacy

Reports include directory paths, account names, SIDs, permissions, and any recorded errors. Treat each report as sensitive access-control information and store or share it accordingly. The generated HTML embeds its data and assets and makes no network requests when opened.

## Release information

- [Release notes](docs/RELEASE_NOTES_1.0.0.md)

## License

This project is licensed under the GNU General Public License v3.0 only (`GPL-3.0-only`); see [LICENSE](LICENSE). The license permits commercial use and sale. Distribution of modified versions must comply with GPLv3, including providing the corresponding source code to recipients.

<p align="center">
  <img src="Assets/FilePermissionReporter.png" alt="File Permission Reporter 图标" width="112">
</p>

<h1 align="center">File Permission Reporter</h1>

<p align="center">
  检查 Windows 文件夹权限，并生成内容完整、可离线查看的 HTML 报告。
</p>

<p align="center"><strong>版本 1.0.0 · .NET 10 · WPF · Windows x64 · 英文与简体中文</strong></p>

<p align="center"><a href="README.md">English</a> · 简体中文</p>

File Permission Reporter 扫描 NTFS 访问控制条目（ACE），并逐目录展示采集到的权限规则。工具会保留各条规则的独立信息，帮助管理员检查当前配置，避免将采集到的规则误认为用户的最终有效访问权限。

## 功能

- 扫描文件夹树；默认扫描深度为 3 层，根目录计为第 1 层，也可选择无限深度。
- 查看 Allow 和 Deny 规则、显式和继承条目、原始权限、权限掩码、账号名及 SID。
- 可选解析并展开 Active Directory 组；使用当前 Windows 登录身份解析嵌套组，并防止嵌套组循环。
- 可选采集本机上与所选目录匹配的共享权限。扫描 UNC 路径时不会查询远程 SMB 共享权限。
- 在 WPF 工作台中浏览和搜索结果，并将其保存为单个离线 HTML 文件；样式、脚本和扫描数据均内嵌在报告中。
- 工作台和报告均支持英文与简体中文。

## 权限语义与范围

报告展示从各目录采集到的 ACL 规则。它**不会**计算最终有效访问权、合并 Allow 与 Deny 规则，也不会判断某个用户能否打开某个文件。

AD 和 SMB 采集均为可选功能。AD 查询使用当前 Windows 登录身份；程序不会索取或保存另一组凭据。SMB 查询仅限本机共享。扫描 UNC 路径时仍可读取 NTFS 规则，但不会查询远程共享权限。

## 开始使用

### 从源码运行

需要 Windows 桌面体验环境和 .NET 10 SDK。只有启用 AD 组展开时，才需要使用 AD 域身份登录。

```powershell
dotnet run
```

在程序中选择文件夹、设置扫描深度和可选采集项，然后开始扫描；扫描结束后即可保存报告。

### 构建便携包

在 Windows 上进入项目根目录并运行：

```powershell
.\publish-portable.ps1
```

脚本会生成 `artifacts/FilePermissionReporter-win-x64.zip`。解压后运行 `FilePermissionReporter.exe` 即可；程序包为自包含版本，无需另行安装 .NET Desktop Runtime。脚本会清理并重建发布目录，只有发布成功后才会替换 ZIP。包内概览见[1.0.0 版本发布说明](docs/RELEASE_NOTES_1.0.0.md)。

### 运行验收测试

在安装了 .NET 10 SDK 的 Windows 上运行：

```powershell
dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release
```

## 系统要求与限制

- Windows x64 桌面体验环境。Windows Server Core 不支持运行此 WPF 图形界面。
- SMB 采集需要系统提供 Windows SMB PowerShell cmdlet。
- 真实 Active Directory 和 SMB 环境、错误与取消场景、大型报告交互以及 Windows Server 版本矩阵尚未全部完成端到端验证。

## 报告与隐私

报告包含目录路径、账号名、SID、权限规则及记录到的错误信息。请将报告视为敏感的访问控制数据，并妥善保存和分享。生成的 HTML 内嵌所需数据与资源，打开时不会发出网络请求。

## 版本信息

- [1.0.0 版本发布说明](docs/RELEASE_NOTES_1.0.0.md)

## 许可证

本项目仅依据 GNU 通用公共许可证第 3 版（GPL-3.0-only）授权，详见 [LICENSE](LICENSE)。该许可证允许商业使用和销售。分发修改版本时必须遵守 GPLv3，包括向接收者提供相应源代码。

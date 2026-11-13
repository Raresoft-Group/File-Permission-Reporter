# 编译 exe 说明

本文说明 File Permission Reporter 可执行文件（`FilePermissionReporter.exe`）的输出位置、编译要求和编译步骤。

## 输出位置

编译后的 exe 位于项目文件夹下的以下路径：

```text
<项目文件夹>\artifacts\publish\win-x64\FilePermissionReporter.exe
```

例如项目位于 `G:\Codex_Projects\File Permission Reporter` 时，完整路径为 `G:\Codex_Projects\File Permission Reporter\artifacts\publish\win-x64\FilePermissionReporter.exe`。

同一目录下的 `README.md` 为发布包附带的使用说明。`artifacts/` 已在 `.gitignore` 中忽略，编译产物不会被提交到 Git。

每次编译都会清空并重建 `artifacts\publish\win-x64\`，因此不要把需要保留的文件放在该目录中。

## 产物规格

| 项目 | 规格 |
| --- | --- |
| 目标平台 | Windows x64（`win-x64`） |
| 运行时 | 自包含，目标机器无需安装 .NET |
| 文件形式 | 单个 exe 文件（`PublishSingleFile`） |
| 压缩 | 启用单文件压缩（`EnableCompressionInSingleFile`） |
| 调试符号 | 不包含（`DebugType=None`） |
| 大小 | 约 65 MB |

## 编译要求

编译机器需要满足以下条件：

- 已安装 **.NET 10 SDK**（`dotnet --version` 应以 `10.` 开头）。
- 能访问 NuGet 源，以便还原 `System.DirectoryServices.AccountManagement` 等依赖包。
- 推荐在 **Windows** 上编译。如果在 Linux 等非 Windows 系统上编译 WPF 项目，需要额外加上 `-p:EnableWindowsTargeting=true`，此时生成的是可编译但未在 Windows 上运行验证的产物。

## 编译步骤

### 方式一：使用项目脚本（推荐，Windows PowerShell）

在项目根目录执行：

```powershell
.\publish-portable.ps1
```

脚本会清理 `artifacts\publish\win-x64\`，发布 exe，并额外生成 ZIP 文件 `artifacts\FilePermissionReporter-win-x64.zip`。脚本会检查输出路径位于 `artifacts` 之下，并拒绝经过目录联接或符号链接等重解析点，以免清理或写入被重定向到其他位置。

### 方式二：手动发布（Windows 或其他系统）

在项目根目录执行以下命令：

```bash
dotnet publish FilePermissionReporter.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output artifacts/publish/win-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false
```

在 Windows PowerShell 中，将行尾的反斜杠换行改为反引号 `` ` `` 即可，或写成单行。非 Windows 系统编译时，在命令末尾追加 `-p:EnableWindowsTargeting=true`。

## 编译后检查

1. 确认文件存在：`artifacts\publish\win-x64\FilePermissionReporter.exe`。
2. 在 Windows 上双击或运行该 exe，确认主窗口能打开。
3. 运行测试：`dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release`。测试依赖 Windows 桌面运行时，只能在 Windows 上运行。

## 当前状态

截至 2026-11-13，最新一次 exe 在 Linux 沙箱中用上述发布参数编译成功，未在 Windows 上运行验证。`dotnet test` 因测试宿主需要 Windows 桌面运行时，尚未在 Windows 上通过。

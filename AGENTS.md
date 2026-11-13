# File Permission Reporter 工作指南

本文适用于仓库内所有代码、测试、构建和文档变更。项目面向 Windows 桌面运行；不要把当前未验证的环境假设写成已支持能力。

## 产品和技术边界

- 应用是 `.NET 10 + WPF` Windows 桌面程序，当前便携发布目标为 `win-x64` 自包含单文件 ZIP。
- NTFS ACL 按规则呈现；工具不计算用户的最终有效访问权，也不合并 Allow、Deny、显式或继承规则。
- AD 组展开默认关闭，使用当前 Windows 登录身份；SMB 共享权限只查询本机共享。UNC 扫描不查询远程 SMB 权限。
- 报表必须是内嵌 CSS、JavaScript 和数据的离线 HTML，不得引入 CDN、外部脚本、Node.js 运行时、HTTP 服务端或前端框架运行时。
- 界面与报表文本支持简体中文和英文。新增可见文本时要同时更新两种语言。
- 许可目标是允许商业客户使用，但禁止他人修改后再商业销售；许可证尚未定稿前，不要向公共 remote 推送源码。非商业用途限制不符合用户最新要求；公开源码也不等于 OSI 定义的开源软件。
- `.unlighthouse/` 包含本地生成的 SQLite/浏览器数据，必须保持在版本控制之外。公开推送前检查 `git status` 和待推送文件清单，不提交报告、扫描结果或本地数据库。

## 跨协作者工作

- 每次新工作先读根目录 `PROJECT_STATUS.md` 和 `docs/COLLABORATION.md`，确认共享工作区和当前文件负责人；Claude 侧入口见 `CLAUDE.md`。
- 开始编辑前登记任务和文件范围，结束时按协作约定记录实际改动、实际验证和未完成项。状态台账应串行更新，不能覆盖其他协作者的新状态。
- 不要假设 Claude Desktop 自动拥有本地路径访问权，也不要假设本项目已有 Git remote。协作接入事实见 `PROJECT_STATUS.md`；跨环境协作前先确认同一工作区或共同 remote。
- 公共 GitHub 目标由用户指定为 `Raresoft-Group/File-Permission-Reporter`。当前 GitHub 连接创建仓库被拒；不要改推个人仓库，也不要通过更换账号、令牌或权限范围绕过组织拒绝。

## 代码地图

| 路径 | 用途 |
| --- | --- |
| `MainWindow.xaml`, `MainWindow.xaml.cs` | WPF 工作台、用户输入、进度、树视图、权限表和保存/打开报表 |
| `Models/AuditModels.cs` | 扫描选项、目录、ACL、问题、共享权限和扫描结果模型 |
| `Services/AuditScanner.cs` | 分层枚举目录、采集 ACL、名称解析、跳过重解析点、累计错误与进度 |
| `Services/RightsClassifier.cs` | 常见权限分类以及 Allow/Deny、Explicit/Inherited 和主体类型本地化 |
| `Services/AdGroupResolver.cs` | 可选 AD 主体解析、嵌套组展开、循环保护 |
| `Services/SharePermissionReader.cs` | 可选本机 SMB 查询，通过 Windows PowerShell SMB cmdlet 读取 |
| `Services/ReportGenerator.cs` | 从 `ScanResult` 生成单文件离线 HTML |
| `Resources/Strings.zh-CN.xaml`, `Resources/Strings.en-US.xaml` | WPF 两种语言的资源字典 |
| `tests/AcceptanceTests.cs` | 扫描深度、权限分类、大目录和 HTML 输出验收用例 |
| `publish-portable.ps1` | 发布自包含 x64 程序并生成 ZIP |

## 必须保持的行为

1. 根目录计为第 1 层；默认扫描 3 层；无限层级由 `UnlimitedDepth` 表示。深度改动必须同时检查根目录 ACL 是否仍然采集。
2. 扫描按目录层级执行有界并发；不要改成每个目录都无限创建任务。扫描结果结束时按路径排序，便于树显示和可重复检查。
3. 显式权限与继承权限是独立 ACL 规则。保存 `Identity` 和 `Sid`，并保留原始权限名称、掩码、访问类型、继承标志及传播标志。
4. 常见权限颜色只依据 `RightsClassifier` 分类。未知组合使用中性样式并显示原值；不要推断 ACL 的有效访问结果。
5. ACL、目录枚举、AD 或 SMB 子步骤失败时保留路径和错误并继续可继续的工作；任何问题或取消都应使结果标记为不完整/已取消。取消后已采集的目录和规则仍可生成报告。
6. 默认跳过重解析点。若所选根目录本身是重解析点，读取其 ACL 后不要遍历其子目录。
7. AD 使用当前登录身份，不保存或索取另一组凭据。嵌套组展开要保留层级并使用祖先集合防止循环；主体无法解析时保留原始 ACL 标识并提供问题信息。
8. SMB 仅查询本机共享。更改共享读取逻辑时不能让 UNC 路径静默触发远程查询。
9. HTML 中的路径、账号、SID、错误信息及其他扫描数据均按不可信输入处理。JSON 使用 `JavaScriptEncoder.Default` 序列化；HTML 模板写入动态内容时对进入 `innerHTML` 的值调用 `esc()`，纯文本优先用 `textContent`。禁止把扫描数据拼成可执行脚本。
10. 报告模板不得依赖网络或外部文件。保留 `ReportGenerator.SaveAsync` 输出的 UTF-8 无 BOM 单文件行为。
11. WPF 扫描和文件写入保持异步，不要在 UI 线程使用 `.Wait()` 或 `.Result`。通过 `IProgress<T>` 将进度更新送回界面。
12. 新 UI 文本需更新两个 XAML 字典；新报表文本需更新 `ReportGenerator.GetLabels` 的中英文对象。WPF `DataGridColumn.Header` 在资源字典替换后不会可靠刷新，需在 `MainWindow.UpdateLocalizedHeaders()` 中同步更新。

## 添加功能时的路径

- 新采集字段：先更新 `Models/AuditModels.cs`，再在对应采集服务填充数据；错误、取消、统计和报告序列化也要一并检查。
- 新权限语义：在 `RightsClassifier` 保持原始值和类别分离；同时检查 WPF 颜色、HTML 徽标、权限搜索和明细文本。
- 新 UI 操作：在 `MainWindow.xaml` 添加控件，更新中英文资源，处理禁用/扫描中状态，并保证事件异步不阻塞界面。
- 新报表展示：在 `ReportGenerator` 更新数据投影、两种 `GetLabels`、语义化 DOM 输出和 `esc()` 边界；不要只修改一个语言分支。
- 新行为应优先添加到 `tests/AcceptanceTests.cs` 或相邻测试中。临时目录和报告用 `try/finally` 清理，避免测试写入用户数据目录。

## 常用命令

在 Windows、已安装 .NET 10 SDK 的 PowerShell 中运行：

```powershell
dotnet run
dotnet build .\FilePermissionReporter.sln --configuration Release
dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release
.\publish-portable.ps1
```

发布脚本会清理并重建 `artifacts/publish/win-x64`，覆盖 `artifacts/FilePermissionReporter-win-x64.zip`。脚本限定输出目录在 `artifacts` 下；调整清理逻辑时须继续检查绝对路径边界。

## 文档维护

当前进度见 [`PROJECT_STATUS.md`](PROJECT_STATUS.md)，协作流程见 [`docs/COLLABORATION.md`](docs/COLLABORATION.md)。架构和数据流见 [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)，实现经验及扩展流程见 [`docs/MAINTENANCE.md`](docs/MAINTENANCE.md)，当前测试覆盖和未验证项见 [`docs/VALIDATION.md`](docs/VALIDATION.md)。如果产品约束、依赖、发布目标、协作接入或验证状态改变，应同步更新这些文档和 `README.md`。

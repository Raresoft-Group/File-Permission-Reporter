# File Permission Reporter 项目状态

> 唯一进度台账。开始任务前先读本页；结束或交接时按 `docs/COLLABORATION.md` 更新。日期使用 Asia/Shanghai。

**最后更新：** 2026-11-13

**项目阶段：** 首版 MVP 已实现；真实企业环境验收仍未完成。

- **当前活动任务：** 组织公共仓库已通过网页创建；许可证边界待定，CLI 推送凭据尚未验证。
- **当前工作区：** `G:\Codex_Projects\File Permission Reporter`
- **协作接入：** 用户确认双方访问同一目录。本地 `main` 已有基线提交；公共仓库 `https://github.com/Raresoft-Group/File-Permission-Reporter` 已创建，`origin` 与其匹配。仓库目前为空；`git ls-remote origin` 能访问公开地址，但不能证明身份验证或写入权限。
- **许可证状态：** 允许商业客户使用；禁止他人修改后商业销售。许可证的具体分发边界和许可方名称尚待定稿；纯非商业许可证不符合要求。
- **公开推送门槛：** 用户已确认创建 Public 仓库；License、README 和 `.gitignore` 均未在 GitHub 初始化。源代码尚未推送，等待许可证定稿。网页端创建成功；CLI 使用 `tedxuyuan` 的 keyring PAT，先前 API 创建请求因 `CreateRepository` 权限失败，Git 推送权限还未验证。

## 当前基线

| 项目 | 状态 | 可核对依据 |
| --- | --- | --- |
| WPF 桌面工具 | 已实现 | `MainWindow.xaml`、`MainWindow.xaml.cs` |
| NTFS 分层扫描、深度和有界并发 | 已实现 | `Services/AuditScanner.cs`；接受测试覆盖深度边界和 10,000 子目录扫描 |
| ACL 规则展示与权限统计 | 已实现 | `Models/AuditModels.cs`、`Services/RightsClassifier.cs` |
| 可选 AD/SMB 采集 | 已实现代码，环境验收待做 | `Services/AdGroupResolver.cs`、`Services/SharePermissionReader.cs` |
| 离线 HTML 报表 | 已实现 | `Services/ReportGenerator.cs`；测试检查单文件和不可信文本编码 |
| Release 构建与 x64 便携 ZIP | 已验证生成 | `publish-portable.ps1`、`artifacts/FilePermissionReporter-win-x64.zip`（构建产物被忽略，不是源代码基线） |
| 桌面启动和本地浅层扫描 | 已冒烟验证 | 开发工作站项目目录扫描：深度 1、1 个目录、7 条 ACL、4 个主体 |
| 中英文工作台列标题 | 已检查主要表格 | 权限表与共享表在语言切换后可切换；警告表标题路径做过代码核对，未在 UI 中单独展开确认 |
| 同目录共同工作 | 用户已确认 | Codex 与 Claude Desktop 使用同一物理项目目录；需要按协作指南串行更新共享状态 |
| AD、SMB、Server 版本矩阵 | 未验证 | 详见 `docs/VALIDATION.md` |

**最近一次代码验证记录（2026-11-13）：** Release 构建通过，0 警告/0 错误；接受测试 5/5 通过；发布 ZIP 已生成。之后的协作文档修改没有改变应用代码，也没有在本次文档更新中重跑应用测试。

## 已完成里程碑

- 首版功能实现、测试和 `win-x64` 便携发布脚本。
- 架构、维护经验、验证边界文档：`docs/ARCHITECTURE.md`、`docs/MAINTENANCE.md`、`docs/VALIDATION.md`。
- 跨协作者状态台账和交接约定：本页、`docs/COLLABORATION.md`、`CLAUDE.md`。

## 待办清单

| 优先级 | 状态 | 任务 | 验收依据 | 负责人 |
| --- | --- | --- | --- | --- |
| P0 | 待验证 | 在许可证定稿后确认本机 Git 凭据可向组织仓库写入；CLI 与浏览器登录凭据分开检查 | 用授权的 HTTPS/Git Credential Manager 凭据验证 push 权限；不在文档或聊天中暴露 token | 用户/Codex |
| P0 | 等许可证定稿 | 明确允许的商业使用/服务范围、修改版本分发与销售边界、许可方名称 | LICENSE 文本可准确表达用户范围，README 与仓库元数据一致 | 用户/项目维护者 |
| P1 | 未开始 | 在真实 AD 域验证嵌套组、循环、用户/计算机主体、无法解析、权限不足和取消 | 记录环境与实际结果，必要时补自动化或手工验收说明 | 未分配 |
| P1 | 未开始 | 验证本机 SMB 查询过滤、无权限和无匹配共享；验证 UNC 明确不查询远程共享 | SMB 主机实测结果和 UNC 提示截图/记录 | 未分配 |
| P1 | 未开始 | 验证 ACL 拒绝访问、目录变化、网络中断、重解析点和取消后的部分报表 | 每个场景都检查继续行为、问题路径和报告状态 | 未分配 |
| P2 | 未开始 | 对 10,000+ 目录的 WPF 操作和 HTML 报表搜索/展开做性能验收 | 记录目录形状、耗时、内存与交互；当前测试只覆盖扫描计数 | 未分配 |
| P2 | 未开始 | 在 Windows Server 2016/2019/2022/2025 Desktop Experience 和管理员工作站验证便携包 | 各系统的启动、扫描和打开报告结果；不把 Server Core 纳入首版 GUI 范围 | 未分配 |

## 当前限制和注意事项

- 文档台账不是实时通知。虽然用户已确认双方访问同一目录，每个协作者开始任务前仍应重新读取本页；台账更新要串行进行。
- 本地 Git 已初始化为 `main` 并提交基线，`origin` 指向已创建的组织仓库；公开地址可访问但远端没有 refs，匿名读取不能证明写权限。浏览器端已创建仓库；CLI `gh auth status` 显示 `tedxuyuan` 的 keyring PAT，和网页登录态分开，写权限未验证。源代码推送仍待许可证定稿。
- Claude Desktop 能访问同一目录是用户已确认的工作方式；`CLAUDE.md` 是否自动加载仍取决于 Claude Desktop 的模式和项目配置。
- 不要把真实客户 ACL、路径或 HTML 报告提交到共享源代码仓库。`artifacts/` 已在 `.gitignore` 中忽略。

## 最近交接记录

| 日期 | 协作者 | 变更/结论 | 验证 | 后续 |
| --- | --- | --- | --- | --- |
| 2026-11-13 | Codex | 用户确认后通过网页创建 `Raresoft-Group/File-Permission-Reporter` Public 仓库；本地 `origin` 已匹配；`LICENSE`、README 和 `.gitignore` 均未在 GitHub 初始化 | 仓库页面显示 Public；`git ls-remote origin` 返回无 refs，公开仓库可匿名读取，故未验证 CLI 身份或写权限；`gh auth status` 为 `tedxuyuan` 的 keyring PAT；本地源代码未推送，未运行应用测试 | 定稿许可证；随后验证 Git 写凭据并推送本地基线 |

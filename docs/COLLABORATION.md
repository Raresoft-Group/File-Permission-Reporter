# Codex 与 Claude Desktop 协作约定

## 先说清楚共享的边界

项目状态的唯一记录是仓库根目录的 `PROJECT_STATUS.md`。架构、约束、验证记录分别见 `AGENTS.md` 和 `docs/` 下的指南。本文件不承诺平台之间自动同步：协作者必须能够读取同一份文件，才能共享进度。

当前已知情况：用户确认 Codex 与 Claude Desktop 访问同一个 `G:\Codex_Projects\File Permission Reporter` 目录。用户指定的组织 URL 是 `https://github.com/Raresoft-Group`，准确 login 为 `Raresoft-Group`。用户确认后，已通过网页创建 Public 仓库 `https://github.com/Raresoft-Group/File-Permission-Reporter`；本地 `main` 有基线提交，`origin` 指向该仓库。GitHub 仓库目前为空；`git ls-remote origin` 可访问公开地址但没有 refs，这不验证 CLI 身份或写入权限。创建时没有添加 README、`.gitignore` 或 License；源代码尚未推送。

网页端创建成功说明该浏览器登录态可以在组织下创建仓库；先前 `gh repo create` 失败的 CLI 登录是 `tedxuyuan` 的 keyring PAT（由 `gh auth status` 确认），与网页会话分开。CLI PAT 的组织/仓库权限需要在推送前另行验证；无需为网页端重复申请仓库创建权限。

用户最新说明的许可目标为：商业客户可以使用软件，但不允许他人修改后再商业销售。准确允许的收费服务范围、修改版本的免费分发条件和许可方名称尚待定稿；许可证不能继续按纯非商业用途选择。

## 推荐接入方式

| 方式 | 适用情况 | 优点 | 限制 |
| --- | --- | --- | --- |
| 同一台 Windows 上打开同一目录（已确认） | Codex 与 Claude Desktop 访问 `G:\Codex_Projects\File Permission Reporter` | 文件状态和本地 Git 基线直接共享，无需复制上下文 | 必须串行编辑重叠文件；Claude Desktop 未必自动读取 `AGENTS.md` |
| 两边使用同一个 Git remote 和独立分支 | Codex 与 Claude Desktop 使用不同工作区或机器，或需要可靠回滚/审阅 | 有变更历史、分支隔离、diff 和合并冲突处理 | 远端目前为空，许可证定稿并推送基线后才能进行远端协作 |
| Claude Desktop 只有聊天/附件能力 | 无法直接读取本地路径 | 可通过附加文档和传递 patch 协作 | 进度不是实时共享；每次交接都要重新传递变更，不适合作为无障碍日常协作 |

同一目录访问已由用户确认，所以保存到该目录的状态文件和本地 Git 历史可以共享；公共远端已创建且公开 URL 可访问，目前尚无远端分支。公开地址读取不验证 CLI 凭据；浏览器端创建成功，CLI 写入权限仍待验证。**文档和 `CLAUDE.md` 不能替代 GitHub 组织权限或桌面端项目配置。**

### 给 Claude Desktop 的项目指令

如果 Claude Desktop 的当前项目支持项目级 instructions/knowledge，将下面内容加入该项目；并让它能读取下列仓库文件。如果它不能读取本地文件，就先附加这些文件或使用可访问本地仓库的代码功能，再开始修改：

```text
这是 File Permission Reporter 项目。开始前读取 AGENTS.md、PROJECT_STATUS.md、docs/COLLABORATION.md 和 README.md；修改扫描、UI、报表或测试前再读对应的 docs/ARCHITECTURE.md、docs/MAINTENANCE.md、docs/VALIDATION.md。AGENTS.md 是项目约束的唯一权威来源，PROJECT_STATUS.md 是进度台账。开始前登记负责人、任务和文件范围；不要同时修改 Codex/其他协作者正在改的文件。结束时更新状态台账，准确记录改了哪些文件、实际执行的命令/结果和未验证事项。不要声称看到了未附加或当前会话无法读取的文件。
```

根目录 `CLAUDE.md` 是 Claude Code 类环境的入口提示。标准 Claude Desktop 对它是否自动加载取决于实际产品模式和项目设置，必须通过让 Claude 复述当前 `PROJECT_STATUS.md` 的活动任务来验证；仅放入该文件不代表接入成功。

## 任务领取和交接流程

### 开始工作

1. 重新读取 `PROJECT_STATUS.md`，确认没有别人正在编辑你要改的文件，并查看当前测试/验证基线。
2. 在“当前活动任务”或待办表中登记协作者、任务、目标文件和预计验收结果。不要两位协作者同时编辑 `PROJECT_STATUS.md`；进度台账更新应串行完成。
3. 优先把任务切成互不重叠的文件集。若必须同时触及 `MainWindow.xaml`、`MainWindow.xaml.cs`、`ReportGenerator.cs` 等共享热点文件，先交接当前编辑权，或使用独立 Git 分支/工作区。
4. 如果状态台账和工作区现状冲突，以可读取到的源文件和实际命令结果为准；先修正台账再继续。

### 完成或暂停工作

更新 `PROJECT_STATUS.md` 的活动任务、待办状态和最近交接记录，并给出：

```text
协作者：Codex / Claude / 用户
任务：简述目标
修改文件：逐项路径
验证：实际运行的命令及通过/失败/未运行
未完成或风险：具体项目，或“无”
下一步：负责人及可验收结果
```

只记录真实跑过的命令。没有运行测试就写“未运行”，不要用“应该通过”代替证据。遇到合并冲突或发现对方仍在编辑同一文件，先停止覆盖写入，交换变更范围后再继续。

## Git 与变更安全

- 在同一工作目录协作时，避免同时保存同一文件；保存前重读文件以避免覆盖新内容。
- 若使用独立工作区，先配置共享 Git remote，并为 Codex、Claude 和用户使用不同分支/工作区；合并前查看完整 diff 并运行对应验证。
- 当前本地 Git 已建立，`origin` 指向 `https://github.com/Raresoft-Group/File-Permission-Reporter.git`。网页端创建仓库已成功；CLI 使用独立的 keyring PAT，先前创建 API 请求被拒。许可证未定稿前不要公开推送源码；推送前先确认 Git 凭据有该仓库的内容写权限。不要用个人仓库替代用户指定的组织。
- `.gitignore` 排除了 `bin/`、`obj/` 和 `artifacts/`。生成的发布包不是源码变更；客户扫描产生的 HTML/ACL 数据不应提交或附加到共享会话。
- 涉及用户/客户目录数据时，只使用获准的测试数据；提交给另一协作者的是代码和脱敏的验收结果，不是客户权限清单。

## 进度可见性

`PROJECT_STATUS.md` 是可轮询的项目台账，不会主动把更新推送给另一个应用。每次会话开始和交接时重新读取；长时间任务有实质状态变化时记录阻塞、验证结果或范围变更。README 只链接进度台账，不重复保存另一套状态。

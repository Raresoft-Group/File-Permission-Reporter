# 测试覆盖与验证边界

## 自动化覆盖

运行：

```powershell
dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release
```

当前 `tests/AcceptanceTests.cs` 的 5 项测试覆盖：

| 用例 | 已验证内容 |
| --- | --- |
| `RightsClassifierLabelsCommonRulesAndPreservesUnknownMasks` | Full Control、Modify、Read 分类；自定义组合保留可读原值 |
| `ScanDepthCountsRootAsLevelOne` | 根目录计为第 1 层、指定深度边界、根 ACL 被读取、显式/继承计数加总一致 |
| `UnlimitedScanIncludesEveryNestedDirectory` | 无限层级包含深层目录 |
| `ScanHandlesTenThousandChildDirectories` | 扫描 10,000 个子目录、结果路径唯一且总数正确 |
| `HtmlReportIsStandaloneAndEncodesUntrustedNames` | 生成完整 HTML、保留权限详情、编码恶意目录/身份文本、不引用外部脚本/样式/HTTPS |

## 当前环境验证记录

- Release 解决方案构建成功，0 个警告、0 个错误。
- `dotnet test` 通过 5 项测试。
- `publish-portable.ps1` 生成 `artifacts/FilePermissionReporter-win-x64.zip`；ZIP 内包含单文件 EXE 和 README。
- 发布 EXE 已在当前 Windows 桌面环境启动。对项目目录做过深度 1 的 UI 冒烟扫描，摘要与权限表出现了真实结果；中英文切换后的主界面、权限表和共享表列标题已检查。警告表标题刷新路径已代码核对，但本次没有切换到该表验证实际呈现。

上述桌面扫描使用的是开发工作站项目目录，不是文件服务器兼容性验收；只证明该环境下窗口可以启动并完成一次本地扫描。

## 尚需环境或人工验证的场景

| 范围 | 当前证据 | 后续验证建议 |
| --- | --- | --- |
| AD 组展开 | 未在真实 AD 域执行端到端验证 | 测试嵌套组、循环、未解析账号、访问错误、长查询及取消 |
| SMB 权限采集 | 自动化用例未执行 SMB cmdlet | 在开启 SMB Server 角色且有重叠共享路径的主机上确认过滤、访问错误和无匹配共享 |
| ACL 错误、网络中断、目录变化、取消 | 当前 5 项测试没有故障注入覆盖 | 使用只读测试目录/受控断网条件验证问题路径、继续扫描和部分报告状态 |
| 重解析点 | 当前 5 项测试没有建立 junction/symlink | 验证根链接和子目录链接的计数、跳过和不遍历行为 |
| 10,000+ 节点桌面/HTML 交互 | 10,000 目录扫描有自动化测试；WPF 展开、HTML 报告渲染/搜索没有性能基准 | 使用大而深与大而宽的数据分别测启动、选择、过滤、全部展开和浏览器搜索 |
| 离线浏览器行为 | 测试确认 HTML 不含外部引用；未自动化所有浏览器交互 | 断网打开报告，检查树、详情、搜索、跳转、错误、共享和 AD 成员展示 |
| Windows Server 2016/2019/2022/2025 | 当前机器未逐版本运行 | 在带桌面体验的受支持 x64 系统逐个启动便携包并执行代表性扫描；Server Core 不属于首版 GUI 主机范围 |

修改发布目标、扫描语义或报告脚本时，应把新行为加入自动化测试或在表中记录可重复的人工验收步骤。不要把“目标框架可编译”当作目标 Windows Server 版本的实机证明。

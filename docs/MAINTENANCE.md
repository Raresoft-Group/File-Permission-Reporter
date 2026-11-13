# 维护经验与功能扩展

本文记录实现过程中已观察到的行为和后续扩展时容易遗漏的边界。

## 已验证的实现经验

### WPF 构建成功不等于资源已正确加载

首次启动时，编译成功但窗口因 XAML 引用缺失的 `SurfaceBrush` 在运行时抛出 `XamlParseException`。修复后应重新启动实际发布的 EXE，而不只检查 `dotnet build`。添加或重命名静态资源时，检查引用与定义；至少做一次窗口启动冒烟检查。

### 语言字典切换有 WPF 表格列标题的特殊情况

普通控件使用 `DynamicResource` 能随资源字典变化；`DataGridColumn.Header` 不一定刷新。现在 `MainWindow.UpdateLocalizedHeaders()` 在启动和语言切换时重设权限、共享、问题三张表的标题。新增列时也要更新该方法并在两种语言切换后观察实际界面。

### `FileSystemRights` 文本存在枚举别名

位标志枚举的 `ToString()` 可能把某个值显示为别名，例如 `WriteData` 可能显示为 `CreateFiles`。分类和权限判断应基于掩码/枚举值，不要依赖某个名字必须出现在文本中；需要稳定含义时在测试里断言类别或数值。未知组合要继续显示原始值。

### HTML 输出要在两个边界都防护

数据先进入 `<script>` 中的 JSON，再由浏览器脚本拼成表格。JSON 序列化配置的 `JavaScriptEncoder.Default` 防止数据闭合脚本元素；任何插入 `innerHTML` 的动态路径、账号、错误或权限文本都还需要 `esc()`。能用 `textContent`、`createElement` 设置文本的地方优先使用 DOM 文本接口。测试应使用包含 `<script>` 和事件属性的恶意目录名/身份，而不只检查普通字符。

### 10,000 目录扫描检查不代表整个大报表交互已验证

现有大目录测试真实创建 10,000 个子目录并执行扫描，能发现扫描吞吐/目录计数回归；它没有验证 WPF 全部展开或浏览器在 10,000 节点下的渲染和搜索响应。报告树使用惰性子节点与批量展开，但仍需有大报告浏览器基准后才能宣称交互性能达标。

## 扩展功能的检查清单

### 添加扫描数据

1. 在 `ScanOptions` / `DirectoryRecord` / `PermissionEntry` / `ScanResult` 中确定字段的原始值、展示值、可空性和统计口径。
2. 在扫描器或对应服务采集字段，并明确错误路径、取消行为、重试/继续策略和进度消息。
3. 检查 `MainWindow` 的统计、选中目录详情、搜索及保存流程。
4. 在 `ReportGenerator` 的 payload 和报表展示同步数据；不要只改 WPF 或只改报告。
5. 增加针对边界值、错误和取消的测试。对于 Windows ACL、AD、SMB API，说明测试宿主条件。

### 修改权限展示

1. 保留 `RightsRaw`、`RightsMask`、`AccessType`、`IsInherited`、继承/传播标志，不要用友好文字替代采集值。
2. 检查 `RightsClassifier` 的精确分类规则。常见 Full Control、Modify、Read 使用指定颜色；自定义组合保持中性。
3. 检查本地化标签、桌面表格颜色、HTML 徽标、详情和搜索字段，保证两种语言的行为一致。

### 修改 AD 或 SMB 能力

- AD 读取依赖当前登录环境和可用的域控制器。验证嵌套组、循环、用户/计算机主体、未解析主体、权限不足和超时；原始 ACL 规则不能因展开失败而丢失。
- SMB 代码以 `powershell.exe`、`Get-SmbShare` 和 `Get-SmbShareAccess` 为边界。验证未安装/不可用 cmdlet、无权读取以及本地路径重叠规则；UNC 路径必须维持“只提示、不查远端”的约束。
- 避免把一个没有 AD/SMB 的开发工作站上的成功编译描述为这些功能已端到端验证。

## 并发、取消和性能

- `AuditScanner` 使用逐层并发，`ConcurrentBag` 汇总结果；只在扫描结束时整理顺序。保持有界 `MaxDegreeOfParallelism`，不要在工作项中启动脱离生命周期的后台任务。
- 扫描结果及报告输入都驻留内存，资源消耗会随目录数和 ACL 条目数增长。新增全量索引时评估大目录的额外副本。
- `Parallel.ForEachAsync` 抛出取消后，已经采集的记录仍进入结果，尚未处理的当前层项目不保证被访问。报告状态必须继续显示已取消/不完整。
- 在 WPF 事件处理中使用 `async`/`await` 和 `IProgress<ScanProgress>`；不要将目录枚举、域查询或文件写入放在 UI 线程同步执行。
- HTML 目录索引用迭代方式建立，展开全部以 `requestAnimationFrame` 分批执行；避免把 10,000 节点渲染改成一次递归拼出整棵 DOM。

## 开发与发布命令

```powershell
dotnet run
dotnet build .\FilePermissionReporter.sln --configuration Release
dotnet test .\tests\FilePermissionReporter.Tests.csproj --configuration Release
.\publish-portable.ps1
```

发布 ZIP 位于 `artifacts/FilePermissionReporter-win-x64.zip`，发布目录位于 `artifacts/publish/win-x64`。这两个路径被 `.gitignore` 忽略。脚本清理发布子目录并覆盖 ZIP；先确认路径和脚本内容，再修改清理范围。

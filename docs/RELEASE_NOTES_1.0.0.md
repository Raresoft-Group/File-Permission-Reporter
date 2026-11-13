# File Permission Reporter 1.0.0

Release date: 2026-11-13

## English

The first 1.0 release provides a Windows desktop application for reviewing NTFS directory access rules and producing a self-contained HTML report.

- Scan a directory tree with a configurable depth and bounded concurrency.
- Review Allow and Deny entries, explicit and inherited rules, original rights, masks, identities, and SIDs without calculating effective access.
- Optionally resolve nested Active Directory groups using the current Windows logon.
- Optionally collect permissions for matching shares on the local computer; UNC scans do not query remote SMB permissions.
- Search and browse results in the WPF app and in the offline report.
- Use the app and report in English or Simplified Chinese.
- Build a self-contained `win-x64` ZIP package that includes both README files, the GPL license, and the public documentation.

## 简体中文

1.0 首个正式版本提供 Windows 桌面程序，用于查看 NTFS 目录权限规则并生成独立 HTML 报告。

- 按可配置深度和有界并发扫描目录树。
- 查看 Allow/Deny、显式/继承规则、原始权限、权限掩码、账号名和 SID；不计算最终有效访问权。
- 可选使用当前 Windows 登录身份解析嵌套 Active Directory 组。
- 可选采集本机匹配共享的权限；扫描 UNC 路径时不查询远程 SMB 共享权限。
- 可在 WPF 工作台和离线报告中浏览、搜索结果。
- 工作台与报告均支持英文和简体中文。
- 提供自包含 `win-x64` ZIP，内含中英文 README、GPL 许可证和公开项目文档。

Licensed under GNU GPL v3.0 only (`GPL-3.0-only`).

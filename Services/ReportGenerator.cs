using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;
using FilePermissionReporter.Models;

namespace FilePermissionReporter.Services;

public static class ReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Default
    };

    // english selects the language shown when the report opens. Both languages are embedded,
    // and the page can switch between them without loading anything else.
    public static async Task SaveAsync(ScanResult result, string outputPath, bool english, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            rootPath = result.RootPath,
            startedAt = result.StartedAt,
            durationSeconds = Math.Round(result.Duration.TotalSeconds, 2),
            status = result.Status,
            requestedDepth = result.RequestedDepth,
            unlimitedDepth = result.UnlimitedDepth,
            skippedReparsePoints = result.SkippedReparsePoints,
            resolvedAdMemberCount = result.ResolvedAdMemberCount,
            shareStatusMessage = string.IsNullOrWhiteSpace(result.ShareStatusMessage)
                ? null
                : new { zh = LocalizeShareStatus(result.ShareStatusMessage, english: false), en = result.ShareStatusMessage },
            directoryCount = result.DirectoryCount,
            permissionCount = result.PermissionCount,
            uniquePrincipalCount = result.UniquePrincipalCount,
            explicitPermissionCount = result.ExplicitPermissionCount,
            inheritedPermissionCount = result.InheritedPermissionCount,
            directories = BuildTree(result.Directories),
            shares = result.SharePermissions.Select(share => new
            {
                share.ShareName,
                share.SharePath,
                share.Account,
                share.AccessRight,
                share.AccessType
            }),
            issues = result.Issues
        };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var labels = JsonSerializer.Serialize(new { zh = GetLabels(english: false), en = GetLabels(english: true) }, JsonOptions);
        var html = HtmlTemplate
            .Replace("__HTML_LANG__", english ? "en" : "zh-CN", StringComparison.Ordinal)
            .Replace("__INIT_LANG__", english ? "\"en\"" : "\"zh\"", StringComparison.Ordinal)
            .Replace("__TITLE__", english ? "File Permission Report" : "文件权限审计报告", StringComparison.Ordinal)
            .Replace("__UI__", labels, StringComparison.Ordinal)
            .Replace("__DATA__", json, StringComparison.Ordinal);

        await File.WriteAllTextAsync(outputPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    private static List<ReportDirectory> BuildTree(IReadOnlyList<DirectoryRecord> records)
    {
        var nodes = records.ToDictionary(
            record => record.Path,
            record => new ReportDirectory
            {
                Id = record.Path,
                Name = record.Name,
                Path = record.Path,
                ParentPath = record.ParentPath,
                Level = record.Level,
                AclRead = record.AclRead,
                ErrorMessage = record.ErrorMessage,
                Permissions = record.Permissions.Select(permission => new ReportPermission
                {
                    Identity = permission.Identity,
                    Sid = permission.Sid,
                    RightsRaw = permission.RightsRaw,
                    RightsMask = permission.RightsMask,
                    RightsCategory = permission.RightsCategory,
                    AccessType = permission.AccessType,
                    IsInherited = permission.IsInherited,
                    InheritanceFlags = permission.InheritanceFlags,
                    PropagationFlags = permission.PropagationFlags,
                    IsAdGroup = permission.IsAdGroup,
                    GroupResolved = permission.GroupResolved,
                    GroupMembers = BuildMemberTree(permission.GroupMembers)
                }).ToList()
            },
            StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.Values)
        {
            if (node.ParentPath.Length > 0 && nodes.TryGetValue(node.ParentPath, out var parent))
                parent.Children.Add(node);
        }

        foreach (var node in nodes.Values)
            node.Children.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        return nodes.Values.Where(node => node.ParentPath.Length == 0).ToList();
    }

    private static List<ReportMember> BuildMemberTree(IEnumerable<AdMemberNode> members) => members.Select(member => new ReportMember
    {
        Account = member.Account,
        DisplayName = member.DisplayName,
        ObjectType = member.ObjectType,
        Members = BuildMemberTree(member.Members)
    }).ToList();

    private static string LocalizeShareStatus(string message, bool english)
    {
        if (string.IsNullOrWhiteSpace(message) || english)
            return message;
        if (message.StartsWith("Remote SMB", StringComparison.OrdinalIgnoreCase))
            return "未查询远程 SMB 共享权限；本版本仅支持查询本机共享。";
        if (message.StartsWith("Local SMB", StringComparison.OrdinalIgnoreCase))
            return "已采集本机 SMB 共享权限。";
        if (message.StartsWith("Some local SMB", StringComparison.OrdinalIgnoreCase))
            return "部分本机 SMB 共享权限读取失败。";
        return message;
    }

    private static object GetLabels(bool english) => english
        ? new
        {
            title = "File Permission Report",
            scan = "Scan status", complete = "Complete", incomplete = "Incomplete", cancelled = "Cancelled",
            scannedAt = "Scanned", root = "Root directory", depth = "Depth", directories = "Directories",
            rules = "ACL entries", principals = "Unique ACL principals", explicitRules = "Explicit rules",
            inheritedRules = "Inherited rules", adMembers = "Resolved AD members", skippedLinks = "Skipped reparse points",
            tree = "Directories", selectedDirectory = "Selected directory", searchDirectories = "Search directories…",
            searchPermissions = "Search accounts or rights in this folder…", all = "Expand all", none = "Collapse all",
            account = "Account", rights = "Rights", type = "Type", inherited = "Inheritance", inheritedValue = "Inherited", details = "Rule details",
            rawRights = "Raw rights", sid = "SID", inheritanceFlags = "Inheritance flags", propagationFlags = "Propagation flags",
            @explicit = "Explicit", yes = "Yes", no = "No", noRules = "No ACL entries for this directory.",
            issues = "Warnings and errors", shares = "SMB share permissions", shareName = "Share",
            sharePath = "Share path", members = "AD group members", group = "Group", member = "Member",
            matchedMember = "Matched group member", openFolder = "Directory", loadMore = "Show more results",
            noMatches = "No matching results.", notCollected = "Not collected", footer = "This report shows ACL rules and group membership. It does not calculate effective access.", developedBy = "Developed by ", noIssues = "No warnings or errors.", includeSubfolders = "Include subfolders", searchResults = "Search results",
            stage = "Stage", path = "Path", message = "Message", accessRight = "Share right",
            reportStatus = "Report status", noShares = "No matching local share permissions were found.",
            groupNotResolved = "AD group details were unavailable; the original ACL identity is retained.",
            fullControl = "Full Control", modify = "Modify", rightsRead = "Read", allow = "Allow", deny = "Deny",
            objUser = "User", objComputer = "Computer", objPrincipal = "Principal"
        }
        : new
        {
            title = "文件权限审计报告",
            scan = "扫描状态", complete = "完整", incomplete = "不完整", cancelled = "已取消",
            scannedAt = "扫描时间", root = "根目录", depth = "扫描层级", directories = "扫描目录数",
            rules = "ACL 权限条目", principals = "唯一 ACL 主体数", explicitRules = "显式权限数",
            inheritedRules = "继承权限数", adMembers = "已解析 AD 成员数", skippedLinks = "跳过的重解析点",
            tree = "目录", selectedDirectory = "当前目录", searchDirectories = "搜索目录…",
            searchPermissions = "搜索当前目录账号或权限…", all = "全部展开", none = "全部收起",
            account = "账号", rights = "权限", type = "类型", inherited = "继承状态", inheritedValue = "继承", details = "权限明细",
            rawRights = "原始权限", sid = "SID", inheritanceFlags = "继承标志", propagationFlags = "传播标志",
            @explicit = "显式", yes = "是", no = "否", noRules = "此目录没有 ACL 权限条目。",
            issues = "警告与错误", shares = "SMB 共享权限", shareName = "共享",
            sharePath = "共享路径", members = "AD 组成员", group = "组", member = "成员",
            matchedMember = "匹配到组成员", openFolder = "目录", loadMore = "显示更多结果",
            noMatches = "没有匹配项。", notCollected = "未采集", footer = "本报告显示 ACL 规则和组成员关系，不计算最终有效访问权。", developedBy = "开发者：", noIssues = "没有警告或错误。", includeSubfolders = "包含子文件夹", searchResults = "搜索结果",
            stage = "阶段", path = "路径", message = "说明", accessRight = "共享权限",
            reportStatus = "报表状态", noShares = "没有找到覆盖此目录的本机共享权限。",
            groupNotResolved = "无法获取 AD 组详情；已保留原始 ACL 主体。",
            fullControl = "完全控制", modify = "修改", rightsRead = "读取", allow = "允许", deny = "拒绝",
            objUser = "用户", objComputer = "计算机", objPrincipal = "主体"
        };

    private sealed class ReportDirectory
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string ParentPath { get; init; } = "";
        public int Level { get; init; }
        public bool AclRead { get; init; }
        public string ErrorMessage { get; init; } = "";
        public List<ReportPermission> Permissions { get; init; } = [];
        public List<ReportDirectory> Children { get; } = [];
    }

    private sealed class ReportPermission
    {
        public string Identity { get; init; } = "";
        public string Sid { get; init; } = "";
        public string RightsRaw { get; init; } = "";
        public int RightsMask { get; init; }
        public string RightsCategory { get; init; } = "custom";
        public string AccessType { get; init; } = "";
        public bool IsInherited { get; init; }
        public string InheritanceFlags { get; init; } = "";
        public string PropagationFlags { get; init; } = "";
        public bool IsAdGroup { get; init; }
        public bool GroupResolved { get; init; }
        public List<ReportMember> GroupMembers { get; init; } = [];
    }

    private sealed class ReportMember
    {
        public string Account { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string ObjectType { get; init; } = "";
        public List<ReportMember> Members { get; init; } = [];
    }

    private const string HtmlTemplate = """
        <!doctype html>
        <html lang="__HTML_LANG__">
        <head>
          <meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
          <title>__TITLE__</title>
          <style>
            :root{color-scheme:light;--ink:#17243b;--muted:#718096;--line:#e2e8f0;--panel:#fff;--wash:#f3f6fb;--blue:#3157c8;--good:#26834a;--warn:#b45d00;--bad:#b42318;--full:#ff4d4f;--modify:#fa8c16;--read:#52c41a}
            *{box-sizing:border-box}body{margin:0;height:100vh;overflow:hidden;display:flex;flex-direction:column;background:var(--wash);color:var(--ink);font:14px/1.45 "Segoe UI",system-ui,sans-serif}button,input{font:inherit}button{cursor:pointer}.top{background:linear-gradient(135deg,#14233e 0%,#1d3461 100%);color:#fff;margin:0;padding:22px 32px;flex:none;min-height:0;display:flex;align-items:center;justify-content:space-between;gap:24px}.top h1{margin:0;font-size:22px;font-weight:700;line-height:1.25}.sub{color:var(--muted);font-size:12px}.top .sub{color:#bdc9dc;margin-top:8px;font-size:13px;line-height:1.4;word-break:break-all}.top-right{display:flex;align-items:center;gap:12px}.status{padding:6px 12px;border-radius:999px;background:#3157c8;font-weight:700}.status.bad{background:#a83a36}.lang-btn{border:0;border-radius:7px;padding:6px 12px;background:#fff;color:#14233e;font-weight:700}.stats{display:grid;grid-template-columns:repeat(7,minmax(130px,1fr));gap:10px;padding:16px 20px}.card,.panel{background:var(--panel);border:1px solid var(--line);border-radius:12px;box-shadow:0 3px 12px #17243b0a}.card{padding:12px 14px;min-height:75px}.card .label{color:var(--muted);font-size:12px}.card .value{font-size:18px;font-weight:700;margin-top:5px;word-break:break-word}.meta{display:flex;gap:10px;padding:0 20px 14px;flex-wrap:wrap}.pill{background:#e9eefb;color:#33486f;border-radius:6px;padding:5px 8px;font-size:12px}.layout{display:grid;grid-template-columns:minmax(280px,32%) 1fr;gap:12px;padding:0 20px 18px;flex:1 1 0;min-height:320px}.panel{overflow:hidden;display:flex;flex-direction:column;min-height:0}.panel-head{padding:14px 16px;border-bottom:1px solid var(--line);font-weight:700;display:flex;align-items:center;justify-content:space-between;gap:8px}.search-row{padding:10px 12px;display:flex;gap:7px;border-bottom:1px solid var(--line)}.scope-toggle{display:flex;align-items:center;gap:5px;flex:none;white-space:nowrap;font-size:13px;color:var(--muted);cursor:pointer}.search-row #permissionSearch{flex:1 1 auto}input[type=search]{min-width:0;width:100%;padding:8px 10px;border:1px solid #cdd6e3;border-radius:7px;outline-color:var(--blue)}.small-btn{border:1px solid var(--line);background:#fff;border-radius:7px;padding:6px 9px;color:#33486f;white-space:nowrap}.tree-area{overflow:auto;padding:8px 8px 18px;flex:1}.tree,.tree ul{list-style:none;margin:0;padding-left:16px}.tree{padding-left:0}.tree-row{display:flex;align-items:center;gap:4px;min-height:30px}.twisty{width:24px;height:24px;border:0;background:transparent;color:#67768d}.node-select{border:0;background:transparent;text-align:left;color:var(--ink);padding:4px 6px;border-radius:5px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1}.node-select:hover,.node-select.selected{background:#eaf0ff;color:#193fae}.tree-children.hidden{display:none}.tree-search-results{max-height:180px;overflow:auto;border-bottom:1px solid var(--line);padding:5px 10px}.hit-link{display:block;width:100%;border:0;background:transparent;text-align:left;padding:6px;color:#28427b;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.hit-link:hover{background:#f0f4fb}.detail-top{padding:12px 16px;background:#fafbfd;border-bottom:1px solid var(--line);font-size:13px;color:var(--muted);word-break:break-all}.table-wrap{overflow:auto;flex:0 1 auto;min-height:120px;max-height:40vh}table{border-collapse:collapse;width:100%;min-width:680px}th,td{text-align:left;border-bottom:1px solid var(--line);padding:9px 11px;vertical-align:top}th{position:sticky;top:0;background:#f7f9fc;color:#55647a;font-size:12px;z-index:1}td{font-size:13px}.badge{display:inline-block;border-radius:5px;padding:2px 7px;font-weight:650;background:#eef1f6;color:#48566b;white-space:nowrap}.badge.full{background:#fff0f0;color:var(--full)}.badge.modify{background:#fff4e5;color:var(--modify)}.badge.read{background:#effbe8;color:var(--read)}.inherit.explicit{color:#237442}.inherit.inherited{color:#2867b7}.rule-button{border:0;background:transparent;color:var(--blue);padding:0;text-align:left}.subpanel{flex:1 1 auto;min-height:120px;border-top:1px solid var(--line);overflow:auto;padding:12px 16px}.subpanel h3{margin:0 0 8px;font-size:14px}.member-list{margin:0;padding-left:20px}.member-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:8px 20px;padding-left:0;list-style:none}.member-list ul{padding-left:20px}.footer{padding:12px 20px;color:var(--muted);font-size:12px}.footer a{color:#2448AA}.empty{color:var(--muted);padding:22px}.error{color:#8e211b}.path-cell{max-width:360px;overflow-wrap:anywhere}.matches{flex:none;padding:8px 12px;max-height:220px;display:flex;flex-direction:column;overflow:hidden}.matches[hidden]{display:none}.matches-body{flex:1 1 auto;min-height:0;overflow:auto}.matches.resized{max-height:none}.matches-title{font-weight:700;font-size:13px;padding:2px 0 8px}.matches[hidden]+.splitter{display:none}.splitter{flex:none;height:9px;cursor:ns-resize;position:relative;border-bottom:1px solid var(--line);touch-action:none}.splitter::after{content:"";position:absolute;left:50%;top:3px;width:40px;height:3px;margin-left:-20px;border-radius:2px;background:#cdd6e3}.splitter:hover::after,.splitter.dragging::after{background:var(--blue)}.matches table{min-width:700px}.search-hit{border:0;background:none;color:var(--blue);padding:0;text-align:left}.share-wrap,.issue-wrap{margin:0 20px 12px}.share-wrap summary,.issue-wrap summary{cursor:pointer;padding:12px 16px;font-weight:700}.share-wrap table,.issue-wrap table{min-width:600px}.below{padding:0 20px 18px;max-height:28vh;overflow:auto}.none{color:var(--muted);padding:14px}.hit td{background:#fff7d6}.stats,.meta,.bottom-tabs{margin:0 20px 12px;padding:0;min-height:0}.tab-bar{display:flex;gap:4px;border-bottom:1px solid var(--line);padding:0 12px}.tab-btn{border:0;border-bottom:2px solid transparent;background:transparent;padding:10px 14px;margin-bottom:-1px;font-weight:650;color:var(--muted);cursor:pointer}.tab-btn.active{color:var(--ink);border-bottom-color:var(--blue)}.tab-pane[hidden]{display:none}.below,.footer{flex:none}
            @media(max-width:1050px){.stats{grid-template-columns:repeat(4,minmax(120px,1fr))}.layout{grid-template-columns:280px 1fr}}@media(max-width:750px){body{height:auto;overflow:auto}.layout{flex:none}.top{padding:18px 16px;align-items:flex-start}.stats{grid-template-columns:repeat(2,minmax(120px,1fr));padding:12px}.layout{grid-template-columns:1fr;padding:0 12px 12px}.panel{min-height:360px}.meta{padding:0 12px 12px}.below{padding:0 12px 12px}.share-wrap,.issue-wrap{margin:0 12px 10px}}
          </style>
        </head>
        <body>
          <header class="top"><div><h1 id="titleText"></h1><div class="sub" id="rootText"></div></div><div class="top-right"><div id="status" class="status"></div><button id="langToggle" class="lang-btn" type="button"></button></div></header>
          <section class="stats" id="stats"></section><div class="meta" id="meta"></div>
          <main class="layout">
            <section class="panel"><div class="panel-head"><span id="treeTitle"></span><div><button class="small-btn" id="expandAll"></button> <button class="small-btn" id="collapseAll"></button></div></div>
              <div class="search-row"><input id="directorySearch" type="search" autocomplete="off"></div><div class="tree-search-results" id="treeSearchResults" hidden></div>
              <div class="tree-area"><ul class="tree" id="treeRoot"></ul></div>
            </section>
            <section class="panel"><div class="panel-head"><span id="detailTitle"></span></div><div class="search-row"><input id="permissionSearch" type="search" autocomplete="off"><label class="scope-toggle"><input type="checkbox" id="includeSubfolders"> <span id="subfolderLabel"></span></label></div>
              <div class="matches" id="permissionMatches" hidden></div><div class="splitter" id="matchSplitter" aria-hidden="true"></div><div class="detail-top" id="selectedPath"></div>
              <div class="table-wrap" id="permissionTable"></div><section class="subpanel" id="membersPanel" hidden></section>
            </section>
          </main>
          <div class="below"><section class="panel bottom-tabs"><div class="tab-bar" role="tablist"><button class="tab-btn active" id="tabShares" data-tab="shares" role="tab" aria-selected="true"></button><button class="tab-btn" id="tabIssues" data-tab="issues" role="tab" aria-selected="false"></button></div><div class="tab-pane" id="sharesPane" role="tabpanel"><div class="table-wrap" id="sharesTable"></div></div><div class="tab-pane" id="issuesPane" role="tabpanel" hidden><div class="table-wrap" id="issuesTable"></div></div></section></div>
          <footer class="footer"><span id="footerText"></span> · <span id="devLabel"></span><a href="https://en.raresoft.net" target="_blank" rel="noopener noreferrer" title="https://en.raresoft.net">Raresoft</a></footer>
          <script>
          const DATA=__DATA__;
          const UIS=__UI__;
          let LANG=__INIT_LANG__;
          const U=()=>UIS[LANG];
          const loc=(value)=>value&&typeof value==="object"?value[LANG]:value;
          const esc=(value)=>String(value??"").replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;").replaceAll("\"","&quot;").replaceAll("'","&#39;");
          const allDirectories=[];const byId=new Map();const parentById=new Map();const liById=new Map();
          function indexTree(root,parent=null){const pending=[[root,parent]];while(pending.length){const [node,nodeParent]=pending.pop();allDirectories.push(node);byId.set(node.id,node);if(nodeParent)parentById.set(node.id,nodeParent);const children=node.children||[];for(let i=children.length-1;i>=0;i--)pending.push([children[i],node])}}
          for(const root of DATA.directories)indexTree(root);
          const treeRoot=document.getElementById("treeRoot");
          let selected=null;let selectedRuleKey="";let currentPermission=null;let lastDirQuery="";let lastPermQuery="";

          function accessWord(type){return type==="Allow"?U().allow:type==="Deny"?U().deny:type}
          function rightsName(p){return p.rightsCategory==="full"?U().fullControl:p.rightsCategory==="modify"?U().modify:p.rightsCategory==="read"?U().rightsRead:p.rightsRaw}
          function inheritanceName(p){return p.isInherited?U().inheritedValue:U().explicit}
          function objectTypeName(m){return m.objectType==="Group"?U().group:m.objectType==="User"?U().objUser:m.objectType==="Computer"?U().objComputer:U().objPrincipal}
          function statusName(s){return s==="Complete"?U().complete:s==="Cancelled"?U().cancelled:U().incomplete}
          function ruleKeyOf(entry,index){return `${entry.identity}|${entry.rightsRaw}|${entry.accessType}|${entry.isInherited}|${index}`}
          function ruleKeyFor(directory,permission){return ruleKeyOf(permission,(directory.permissions||[]).indexOf(permission))}

          function renderChrome(){
            const u=U();
            document.documentElement.lang=LANG==="en"?"en":"zh-CN";
            document.title=u.title;
            document.getElementById("titleText").textContent=u.title;
            document.getElementById("langToggle").textContent=LANG==="en"?"中文":"EN";
            const statusEl=document.getElementById("status");statusEl.textContent=statusName(DATA.status);statusEl.classList.toggle("bad",DATA.status!=="Complete");
            document.getElementById("rootText").textContent=DATA.rootPath;
            const statItems=[[u.scannedAt,new Date(DATA.startedAt).toLocaleString(LANG==="en"?"en-US":"zh-CN")],[u.directories,DATA.directoryCount],[u.rules,DATA.permissionCount],[u.principals,DATA.uniquePrincipalCount],[u.explicitRules,DATA.explicitPermissionCount],[u.inheritedRules,DATA.inheritedPermissionCount],[u.adMembers,DATA.resolvedAdMemberCount]];
            document.getElementById("stats").innerHTML=statItems.map(([label,value])=>`<article class="card"><div class="label">${esc(label)}</div><div class="value">${esc(value)}</div></article>`).join("");
            const shareMessage=loc(DATA.shareStatusMessage);
            document.getElementById("meta").innerHTML=`<span class="pill">${esc(u.root)}: ${esc(DATA.rootPath)}</span><span class="pill">${esc(u.depth)}: ${DATA.unlimitedDepth?"∞":DATA.requestedDepth}</span><span class="pill">${esc(u.skippedLinks)}: ${DATA.skippedReparsePoints}</span><span class="pill">${esc(u.reportStatus)}: ${esc(statusName(DATA.status))}</span>${shareMessage?`<span class="pill">${esc(u.shares)}: ${esc(shareMessage)}</span>`:""}`;
            document.getElementById("treeTitle").textContent=u.tree;
            document.getElementById("detailTitle").textContent=u.selectedDirectory;
            document.getElementById("expandAll").textContent=u.all;
            document.getElementById("collapseAll").textContent=u.none;
            document.getElementById("directorySearch").placeholder=u.searchDirectories;
            document.getElementById("permissionSearch").placeholder=u.searchPermissions;
            document.getElementById("footerText").textContent=u.footer;
            document.getElementById("devLabel").textContent=u.developedBy;
            document.getElementById("subfolderLabel").textContent=u.includeSubfolders;
          }

          function makeTreeNode(node){const li=document.createElement("li");li.dataset.id=node.id;liById.set(node.id,li);const row=document.createElement("div");row.className="tree-row";const twisty=document.createElement("button");twisty.className="twisty";twisty.dataset.toggle="1";twisty.textContent=(node.children||[]).length?"▸":"";twisty.setAttribute("aria-expanded","false");const select=document.createElement("button");select.className="node-select";select.dataset.select="1";select.textContent=node.name;select.title=node.path;row.append(twisty,select);li.append(row);const ul=document.createElement("ul");ul.className="tree-children hidden";li.append(ul);return li}
          function ensureChildren(node,li){const ul=li.querySelector(":scope > ul");if(li.dataset.loaded==="1")return ul;for(const child of(node.children||[]))ul.append(makeTreeNode(child));li.dataset.loaded="1";return ul}
          function expandNode(node,li){if(!(node.children||[]).length)return;const ul=ensureChildren(node,li);ul.classList.remove("hidden");li.querySelector(":scope > .tree-row .twisty").textContent="▾";li.querySelector(":scope > .tree-row .twisty").setAttribute("aria-expanded","true")}
          function collapseNode(li){const ul=li.querySelector(":scope > ul");if(ul)ul.classList.add("hidden");const b=li.querySelector(":scope > .tree-row .twisty");if(b){b.textContent="▸";b.setAttribute("aria-expanded","false")}}

          function renderPermissionTable(entries,highlightKey=""){
            const host=document.getElementById("permissionTable");const u=U();
            if(!entries.length){host.innerHTML=`<div class="empty">${u.noRules}</div>`;return}
            const rows=entries.map((entry,index)=>{const cat=["full","modify","read"].includes(entry.rightsCategory)?entry.rightsCategory:"";return `<tr data-rule="${encodeURIComponent(ruleKeyOf(entry,index))}"><td><button class="rule-button" data-members="1">${esc(entry.identity)}</button>${entry.isAdGroup?` <span class="badge">${esc(u.group)}</span>`:""}</td><td><span class="badge ${cat}" title="${esc(entry.rightsRaw)}">${esc(rightsName(entry))}</span></td><td>${esc(accessWord(entry.accessType))}</td><td class="inherit ${entry.isInherited?"inherited":"explicit"}">${esc(inheritanceName(entry))}</td><td><button class="rule-button" data-details="1">${esc(u.details)}</button></td></tr>`}).join("");
            host.innerHTML=`<table><thead><tr><th>${esc(u.account)}</th><th>${esc(u.rights)}</th><th>${esc(u.type)}</th><th>${esc(u.inherited)}</th><th></th></tr></thead><tbody>${rows}</tbody></table>`;
            if(highlightKey){const found=[...host.querySelectorAll("tr")].find(row=>decodeURIComponent(row.dataset.rule||"")===highlightKey);if(found){found.classList.add("hit");found.scrollIntoView({block:"center"})}}
          }
          function renderSelected(){
            if(!selected)return;
            document.getElementById("selectedPath").textContent=`${U().selectedDirectory}: ${selected.path}${selected.errorMessage?" · "+selected.errorMessage:""}`;
            renderPermissionTable(selected.permissions||[],selectedRuleKey);
          }
          function selectDirectory(node,ruleKey=""){
            selected=node;selectedRuleKey=ruleKey;currentPermission=null;if(lastPermQuery)renderPermissionMatches(permissionMatches(lastPermQuery));
            document.getElementById("membersPanel").hidden=true;
            document.querySelectorAll(".node-select.selected").forEach(el=>el.classList.remove("selected"));const current=liById.get(node.id)?.querySelector(":scope > .tree-row .node-select");if(current)current.classList.add("selected");
            renderSelected();
          }
          function revealPath(node,ruleKey=""){const chain=[];let current=node;while(current){chain.unshift(current);current=parentById.get(current.id)}for(let i=0;i<chain.length-1;i++){const parent=chain[i];const li=liById.get(parent.id);if(li)expandNode(parent,li)}selectDirectory(node,ruleKey)}
          treeRoot.addEventListener("click",event=>{const li=event.target.closest("li[data-id]");if(!li)return;const node=byId.get(li.dataset.id);if(event.target.closest("[data-toggle]")){const button=event.target.closest("[data-toggle]");if(button.getAttribute("aria-expanded")==="true")collapseNode(li);else expandNode(node,li);return}if(event.target.closest("[data-select]"))selectDirectory(node)});
          document.getElementById("expandAll").addEventListener("click",()=>{const queue=[...DATA.directories];let cursor=0;const batch=()=>{let count=0;while(cursor<queue.length&&count++<150){const node=queue[cursor++];const li=liById.get(node.id);if(li)expandNode(node,li);for(const child of(node.children||[]))queue.push(child)}if(cursor<queue.length)requestAnimationFrame(batch)};batch()});
          document.getElementById("collapseAll").addEventListener("click",()=>document.querySelectorAll("li[data-id]").forEach(li=>collapseNode(li)));

          function renderDirectoryHits(query,limit=200){const host=document.getElementById("treeSearchResults");const u=U();if(!query){host.hidden=true;host.innerHTML="";return}const matches=allDirectories.filter(node=>node.name.toLocaleLowerCase().includes(query)||node.path.toLocaleLowerCase().includes(query));host.hidden=false;const shown=matches.slice(0,limit);host.innerHTML=matches.length?shown.map(node=>`<button class="hit-link" data-hit-id="${encodeURIComponent(node.id)}" title="${esc(node.path)}">${esc(node.path)}</button>`).join("")+(matches.length>limit?`<button class="small-btn" id="moreDirectories">${esc(u.loadMore)} (${limit}/${matches.length})</button>`:""):`<div class="none">${esc(u.noMatches)}</div>`;const more=document.getElementById("moreDirectories");if(more)more.addEventListener("click",()=>renderDirectoryHits(query,limit+200));host.querySelectorAll("[data-hit-id]").forEach(button=>button.addEventListener("click",()=>{const node=byId.get(decodeURIComponent(button.dataset.hitId));if(node)revealPath(node)}))}
          let searchTimer=0;document.getElementById("directorySearch").addEventListener("input",event=>{clearTimeout(searchTimer);const q=event.target.value.trim().toLocaleLowerCase();lastDirQuery=q;searchTimer=setTimeout(()=>renderDirectoryHits(q),120)});

          function flattenMembers(members,groupName,query,out){for(const member of(members||[])){const searchable=`${member.account} ${member.displayName} ${member.objectType} ${objectTypeName(member)}`.toLocaleLowerCase();if(searchable.includes(query))out.push({account:member.displayName||member.account,groupName,member:true});flattenMembers(member.members,member.account,query,out)}}
          function directoryScope(dir,withChildren){const out=[dir];if(withChildren){const pending=[...(dir.children||[])];while(pending.length){const node=pending.pop();out.push(node);pending.push(...(node.children||[]))}}return out}
          function permissionMatches(query){const out=[];for(const directory of(selected?directoryScope(selected,document.getElementById("includeSubfolders").checked):[])){for(const permission of(directory.permissions||[])){const text=`${permission.identity} ${rightsName(permission)} ${permission.rightsRaw} ${permission.rightsMask} ${permission.accessType} ${accessWord(permission.accessType)} ${inheritanceName(permission)} ${permission.isInherited?"inherited explicit":"explicit"}`.toLocaleLowerCase();if(text.includes(query))out.push({directory,permission,account:permission.identity,member:false});const memberHits=[];flattenMembers(permission.groupMembers,permission.identity,query,memberHits);memberHits.forEach(member=>out.push({directory,permission,account:member.account,member:true,groupName:member.groupName}))}}return out}
          function renderPermissionMatches(matches,limit=200){
            const host=document.getElementById("permissionMatches");const u=U();host.hidden=false;const shown=matches.slice(0,limit);const title=`<div class="matches-title">${esc(u.searchResults)} (${matches.length})</div>`;if(!matches.length){host.innerHTML=title+`<div class="none">${esc(u.noMatches)}</div>`;return}
            host.innerHTML=title+`<div class="matches-body"><table><thead><tr><th>${esc(u.path)}</th><th>${esc(u.account)}</th><th>${esc(u.rights)}</th><th>${esc(u.type)}</th><th>${esc(u.inherited)}</th></tr></thead><tbody>${shown.map((match,index)=>`<tr><td class="path-cell"><button class="search-hit" data-result="${index}">${esc(match.directory.path)}</button></td><td>${esc(match.account)}${match.member?`<div class="sub">${esc(u.matchedMember)}: ${esc(match.groupName)}</div>`:""}</td><td>${esc(rightsName(match.permission))}<div class="sub">${esc(match.permission.rightsRaw)}</div></td><td>${esc(accessWord(match.permission.accessType))}</td><td>${esc(inheritanceName(match.permission))}</td></tr>`).join("")}</tbody></table></div>${matches.length>limit?`<button class="small-btn" id="morePermissions">${esc(u.loadMore)} (${limit}/${matches.length})</button>`:""}`;
            host.querySelectorAll("[data-result]").forEach(button=>button.addEventListener("click",()=>{const match=shown[Number(button.dataset.result)];revealPath(match.directory,ruleKeyFor(match.directory,match.permission));showMembers(match.permission)}));const more=document.getElementById("morePermissions");if(more)more.addEventListener("click",()=>renderPermissionMatches(matches,limit+200));
          }
          function renderMemberList(members){const render=(items,top)=>`<ul class="member-list${top?" member-grid":""}">${(items||[]).map(member=>`<li>${esc(member.displayName||member.account)} <span class="sub">${esc(objectTypeName(member))} · ${esc(member.account)}</span>${member.members?.length?render(member.members):""}</li>`).join("")}</ul>`;return render(members,true)}
          function showMembers(permission){
            const panel=document.getElementById("membersPanel");const u=U();
            if(!permission||!permission.isAdGroup){currentPermission=null;panel.hidden=true;return}
            currentPermission=permission;panel.hidden=false;
            panel.innerHTML=`<h3>${esc(u.members)}: ${esc(permission.identity)}</h3>${permission.groupResolved?renderMemberList(permission.groupMembers):`<div class="none">${esc(u.groupNotResolved)}</div>`}`;
          }
          document.getElementById("permissionTable").addEventListener("click",event=>{const row=event.target.closest("tr");if(!row||!selected)return;const index=[...document.querySelectorAll("#permissionTable tbody tr")].indexOf(row);const permission=selected.permissions[index];if(!permission)return;if(event.target.closest("[data-members]")){if(currentPermission===permission){currentPermission=null;document.getElementById("membersPanel").hidden=true}else showMembers(permission)}if(event.target.closest("[data-details]")){const u=U();window.alert(`${u.rights}: ${rightsName(permission)} (${permission.rightsRaw}) [${permission.rightsMask}]\n${u.type}: ${accessWord(permission.accessType)}\n${u.inherited}: ${permission.isInherited?u.inheritedValue:u.explicit}\n${u.inheritanceFlags}: ${permission.inheritanceFlags}\n${u.propagationFlags}: ${permission.propagationFlags}\n${u.sid}: ${permission.sid}`)}});
          document.getElementById("includeSubfolders").addEventListener("change",()=>{if(lastPermQuery)renderPermissionMatches(permissionMatches(lastPermQuery))});
          (function(){
            const splitter=document.getElementById("matchSplitter");const matchesEl=document.getElementById("permissionMatches");const panel=matchesEl.parentElement;let startY=0,startH=0;
            splitter.addEventListener("pointerdown",event=>{if(matchesEl.hidden)return;event.preventDefault();splitter.setPointerCapture(event.pointerId);splitter.classList.add("dragging");startY=event.clientY;startH=matchesEl.getBoundingClientRect().height});
            splitter.addEventListener("pointermove",event=>{if(!splitter.hasPointerCapture(event.pointerId))return;
              // 为权限表和成员面板保留空间：其余区块高度之和以外，权限表至少 120px。
              const others=[...panel.children].filter(el=>el!==matchesEl&&el!==splitter&&el.id!=="permissionTable"&&!el.hidden).reduce((sum,el)=>sum+el.offsetHeight,0);
              const max=Math.max(60,panel.clientHeight-others-splitter.offsetHeight-120);
              const next=Math.min(max,Math.max(60,startH+(event.clientY-startY)));
              matchesEl.classList.add("resized");matchesEl.style.height=next+"px"});
            const stop=event=>{splitter.classList.remove("dragging");if(splitter.hasPointerCapture(event.pointerId))splitter.releasePointerCapture(event.pointerId)};
            splitter.addEventListener("pointerup",stop);splitter.addEventListener("pointercancel",stop);
          })();
          let permissionTimer=0;document.getElementById("permissionSearch").addEventListener("input",event=>{clearTimeout(permissionTimer);const query=event.target.value.trim().toLocaleLowerCase();lastPermQuery=query;permissionTimer=setTimeout(()=>{if(!query){document.getElementById("permissionMatches").hidden=true;return}renderPermissionMatches(permissionMatches(query))},120)});

          let activeBottomTab="shares";
          function selectBottomTab(name){
            activeBottomTab=name;
            for(const tab of document.querySelectorAll(".tab-btn")){const on=tab.dataset.tab===name;tab.classList.toggle("active",on);tab.setAttribute("aria-selected",on?"true":"false")}
            document.getElementById("sharesPane").hidden=name!=="shares";
            document.getElementById("issuesPane").hidden=name!=="issues";
          }
          function renderShares(){
            const u=U();const shares=DATA.shares||[];const message=loc(DATA.shareStatusMessage);
            document.getElementById("tabShares").textContent=u.shares;
            const host=document.getElementById("sharesTable");
            if(!shares.length){host.innerHTML=`<div class="none">${esc(message||u.noShares)}</div>`;return}
            host.innerHTML=`<table><thead><tr><th>${esc(u.shareName)}</th><th>${esc(u.sharePath)}</th><th>${esc(u.account)}</th><th>${esc(u.accessRight)}</th><th>${esc(u.type)}</th></tr></thead><tbody>${shares.map(share=>`<tr><td>${esc(share.shareName)}</td><td>${esc(share.sharePath)}</td><td>${esc(share.account)}</td><td>${esc(share.accessRight)}</td><td>${esc(accessWord(share.accessType))}</td></tr>`).join("")}</tbody></table>`;
          }
          function renderIssues(){
            const u=U();const issues=DATA.issues||[];
            document.getElementById("tabIssues").textContent=`${u.issues} (${issues.length})`;
            const host=document.getElementById("issuesTable");
            if(!issues.length){host.innerHTML=`<div class="none">${esc(u.noIssues)}</div>`;return}
            host.innerHTML=`<table><thead><tr><th>${esc(u.stage)}</th><th>${esc(u.path)}</th><th>${esc(u.message)}</th></tr></thead><tbody>${issues.map(issue=>`<tr class="error"><td>${esc(issue.stage)}</td><td class="path-cell">${esc(issue.path)}</td><td>${esc(issue.message)}</td></tr>`).join("")}</tbody></table>`;
          }
          for(const tab of document.querySelectorAll(".tab-btn"))tab.addEventListener("click",()=>selectBottomTab(tab.dataset.tab));
          selectBottomTab(activeBottomTab);

          function renderAll(){
            renderChrome();
            renderSelected();
            if(currentPermission)showMembers(currentPermission);
            if(lastDirQuery)renderDirectoryHits(lastDirQuery);
            if(lastPermQuery)renderPermissionMatches(permissionMatches(lastPermQuery));
            renderShares();
            renderIssues();
          }
          document.getElementById("langToggle").addEventListener("click",()=>{LANG=LANG==="en"?"zh":"en";renderAll()});

          for(const root of DATA.directories)treeRoot.append(makeTreeNode(root));
          if(DATA.directories.length)selectDirectory(DATA.directories[0]);
          renderAll();
          </script>
        </body>
        </html>
        """;
}

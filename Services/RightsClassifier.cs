using System.Security.AccessControl;

namespace FilePermissionReporter.Services;

public static class RightsClassifier
{
    public static (string Label, string Category) Classify(FileSystemRights rights)
    {
        if (rights == FileSystemRights.FullControl)
            return ("Full Control", "full");
        if (rights == FileSystemRights.Modify)
            return ("Modify", "modify");
        if (rights is FileSystemRights.Read or FileSystemRights.ReadAndExecute)
            return ("Read", "read");
        return (rights.ToString(), "custom");
    }

    public static string Localize(string category, string rawRights, bool english) => category switch
    {
        "full" => english ? "Full Control" : "完全控制",
        "modify" => english ? "Modify" : "修改",
        "read" => english ? "Read" : "读取",
        _ => rawRights
    };

    public static string LocalizeAccessType(string accessType, bool english) => accessType.Equals("Allow", StringComparison.OrdinalIgnoreCase)
        ? english ? "Allow" : "允许"
        : accessType.Equals("Deny", StringComparison.OrdinalIgnoreCase)
            ? english ? "Deny" : "拒绝"
            : accessType;

    public static string LocalizeInheritance(bool inherited, bool english) => inherited
        ? english ? "Inherited" : "继承"
        : english ? "Explicit" : "显式";

    public static string LocalizeObjectType(string objectType, bool english) => objectType switch
    {
        "Group" => english ? "Group" : "组",
        "User" => english ? "User" : "用户",
        "Computer" => english ? "Computer" : "计算机",
        _ => english ? "Principal" : "主体"
    };
}

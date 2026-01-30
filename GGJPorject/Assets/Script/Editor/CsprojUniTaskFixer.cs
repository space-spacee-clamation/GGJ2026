#if UNITY_EDITOR
using System;
using UnityEditor;

/// <summary>
/// Unity 会频繁重生成 csproj，导致 UniTask 的引用丢失，从而影响 IDE/Linter。
/// 这里在生成 Assembly-CSharp.csproj 时自动补一条 UniTask.dll 引用（不使用绝对路径）。
/// </summary>
public class CsprojUniTaskFixer : AssetPostprocessor
{
    private const string UniTaskRef =
@"    <Reference Include=""UniTask"">
      <HintPath>$(MSBuildProjectDirectory)\Library\ScriptAssemblies\UniTask.dll</HintPath>
      <Private>False</Private>
    </Reference>
";

    public static string OnGeneratedCSProject(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content)) return content;
        if (!path.EndsWith("Assembly-CSharp.csproj", StringComparison.OrdinalIgnoreCase)) return content;
        if (content.Contains("Include=\"UniTask\"")) return content;

        // 插在 UnityEngine Reference 后面
        var key = "    <Reference Include=\"UnityEngine\">";
        var idx = content.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return content;

        // 找到该 Reference 的结束 </Reference>
        var endIdx = content.IndexOf("    </Reference>", idx, StringComparison.Ordinal);
        if (endIdx < 0) return content;

        endIdx += "    </Reference>".Length;
        content = content.Insert(endIdx + Environment.NewLine.Length, UniTaskRef);
        return content;
    }
}
#endif



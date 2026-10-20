using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHierarchyViewer
{
    [MenuItem("Tools/Print Scene Hierarchy")]
    public static void PrintHierarchy()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var sb = new StringBuilder();
        sb.AppendLine($"=== SCENE HIERARCHY DUMP: {scene.name} ({roots.Length} Roots) ===");
        
        foreach (var r in roots)
        {
            DumpTransform(r.transform, 0, sb);
        }

        string outPath = "Assets/Editor/Scene_Hierarchy_Dump.txt";
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"Scene hierarchy written to {outPath}");
    }

    private static void DumpTransform(Transform t, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        string activeTag = t.gameObject.activeSelf ? "" : " [INACTIVE]";
        sb.AppendLine($"{indent}• {t.gameObject.name}{activeTag}");
        for (int i = 0; i < t.childCount; i++)
        {
            DumpTransform(t.GetChild(i), depth + 1, sb);
        }
    }
}

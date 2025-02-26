using UnityEngine;
using UnityEditor;
using System.Linq;

public static class ClientAssetPathCollector
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        // 订阅资源刷新事件
        AssetDatabase.importPackageCompleted += OnAssetRefresh;
    }

    private static void OnAssetRefresh(string packageName = "")
    {
        // 延迟一帧执行，避免刷新过程中的问题
        EditorApplication.delayCall += () =>
        {
            CollectUIPaths();
        };
    }

    [MenuItem("Tools/Collect UI Paths")]
    public static void CollectUIPaths()
    {
        // 加载配置文件
        var config = AssetDatabase.FindAssets("t:AssetPathConfig")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<AssetPathConfig>(path))
            .FirstOrDefault();

        if (config == null)
        {
            Debug.LogError("找不到 AssetPathConfig 配置文件");
            return;
        }

        // 加载 AssetsPathTable
        var pathTable = AssetDatabase.FindAssets("t:AssetsPathTable")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<AssetsPathTable>(path))
            .FirstOrDefault();

        if (pathTable == null)
        {
            Debug.LogError("找不到 AssetsPathTable 配置文件");
            return;
        }

        // 清空旧的路径
        pathTable.uiPaths.Clear();

        // 获取所有预制体
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { AssetDatabase.GetAssetPath(config.UIPreafabFolder) });
        
        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            // 检查预制体是否包含 UIBase 组件
            var uiComponent = prefab.GetComponent<UIBase>();
            if (uiComponent != null)
            {
                // 只保留预制体名称
                string prefabName = prefab.name;
                pathTable.uiPaths.Add($"{prefabName}");
                Debug.Log($"找到UI预制体: {prefabName}");
            }
        }

        // 保存配置
        EditorUtility.SetDirty(pathTable);
        AssetDatabase.SaveAssets();
        Debug.Log($"收集完成，共找到 {pathTable.uiPaths.Count} 个UI预制体");
    }
}

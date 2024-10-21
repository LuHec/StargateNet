using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using StargateNet;

public class RegisterStargateNetworkPrefabs : EditorWindow
{
    [MenuItem("Tools/Find NetworkObject Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<RegisterStargateNetworkPrefabs>("Find NetworkObject Prefabs");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Find All Prefabs with NetworkObject and Set PrefabId"))
        {
            FindAllPrefabsWithNetworkObjectAndSetPrefabId();
        }
    }

    [InitializeOnLoadMethod]
    private static void FindAllPrefabsWithNetworkObjectAndSetPrefabId()
    {
        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        List<string> prefabsWithNetworkObject = new();
        List<GameObject> networkPrefabs = new();
        int id = 0;

        foreach (string prefabGUID in allPrefabs)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null && prefab.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                networkObject.PrefabId = id;
                EditorUtility.SetDirty(networkObject);
                networkPrefabs.Add(prefab);
                prefabsWithNetworkObject.Add(prefabPath);
                id++;
            }
        }
        
        // 获取所有 StargateConfig
        string[] allConfigs = AssetDatabase.FindAssets("t:StargateConfig");
        List<string> configPaths = new();

        foreach (string configGUID in allConfigs)
        {
            string configPath = AssetDatabase.GUIDToAssetPath(configGUID);
            StargateConfig config = AssetDatabase.LoadAssetAtPath<StargateConfig>(configPath);

            if (config != null)
            {
                config.NetworkObjects = networkPrefabs;
                configPaths.Add(configPath);
                EditorUtility.SetDirty(config);
            }
        }
        
        AssetDatabase.SaveAssets(); // 保存所有修改过的资产
        AssetDatabase.Refresh(); // 刷新资产数据库

        if (prefabsWithNetworkObject.Count > 0)
        {
            string result = "Found and modified the following prefabs with NetworkObject:\n";
            result += string.Join("\n", prefabsWithNetworkObject);
            Debug.Log(result);
            // EditorUtility.DisplayDialog("Results", result, "OK");
        }
        else
        {
            Debug.Log("No prefabs with NetworkObject found.");
            // EditorUtility.DisplayDialog("Results", "No prefabs with NetworkObject found.", "OK");
        }
    }
}
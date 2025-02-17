using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using StargateNet;

public class RegisterStargateNetworkPrefabsAndInputs : EditorWindow
{
    [MenuItem("Tools/Find NetworkObject Prefabs And Inputs")]
    public static void ShowWindow()
    {
        GetWindow<RegisterStargateNetworkPrefabsAndInputs>("Find NetworkObject Prefabs And Inputs");
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
        long maxStateSize = 0;
        (var inputTypes, var inputBytes) = FindAllTypesOfNetworkInputs();
        foreach (string prefabGUID in allPrefabs)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGUID);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null && prefab.TryGetComponent(out NetworkObject networkObject))
            {
                networkObject.PrefabId = id;
                EditorUtility.SetDirty(networkObject);
                NetworkBehavior[] networkBehaviors = prefab.GetComponentsInChildren<NetworkBehavior>();
                long stateSize = 0;
                foreach (var networkBehavior in networkBehaviors)
                {
                    stateSize += networkBehavior.StateBlockSize;
                }
                maxStateSize = maxStateSize < stateSize ? stateSize : maxStateSize;
                networkPrefabs.Add(prefab);
                prefabsWithNetworkObject.Add(prefabPath);
                id++;
            }
        }
        
        // 获取所有 StargateConfig
        string[] allConfigs = AssetDatabase.FindAssets("t:StargateConfig");

        foreach (string configGUID in allConfigs)
        {
            string configPath = AssetDatabase.GUIDToAssetPath(configGUID);
            StargateConfig config = AssetDatabase.LoadAssetAtPath<StargateConfig>(configPath);

            if (config != null)
            {
                config.NetworkObjects = networkPrefabs;
                config.maxObjectStateBytes  = maxStateSize;
                config.networkInputsTypes = inputTypes;
                config.networkInputsBytes = inputBytes;
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

    private static (List<string>, List<int>) FindAllTypesOfNetworkInputs()
    {
        List<string> allTypesOfNetworkInputs = new List<string>();
        List<int> allBytesOfNetworkInputs = new List<int>();

        // 获取所有程序集（默认加载的程序集）
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        // 遍历程序集，查找继承自 INetworkInput 的类
        foreach (var assembly in assemblies)
        {
            // 获取所有类型
            var types = assembly.GetTypes();
            
            foreach (var type in types)
            {
                // 判断类型是否继承自 INetworkInput
                if (typeof(StargateNet.INetworkInput).IsAssignableFrom(type) && type.IsValueType )
                {
                    allTypesOfNetworkInputs.Add(type.Name);
                    allBytesOfNetworkInputs.Add(Marshal.SizeOf(type));
                }
            }
        }

        // 打印结果
        foreach (var typeName in allTypesOfNetworkInputs)
        {
            Debug.Log(typeName);
        }

        return (allTypesOfNetworkInputs, allBytesOfNetworkInputs);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetPathConfig", menuName = "Assets/AssetPathConfig")]
public class AssetPathConfig : ScriptableObject
{
    [Tooltip("拖拽文件夹到这里")]
    public DefaultAsset UIPreafabFolder;
}

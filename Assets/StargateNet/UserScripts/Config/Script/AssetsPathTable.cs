using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetsPathTable", menuName = "Assets/AssetsPathTable")]
public class AssetsPathTable : ScriptableObject
{
    public List<string> uiPaths = new List<string>();
}

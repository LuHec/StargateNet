using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAllyPanel : UIBase
{
    public GameObject allyTagPrefab;
    public GameObject enemyTagPrefab;
    private Camera mainCamera;
    private Dictionary<int, GameObject> allyObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, Transform> targetTransforms = new Dictionary<int, Transform>();

    protected override void OnInit()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // 在LateUpdate中更新所有标签位置
        foreach (var kvp in targetTransforms)
        {
            if (allyObjects.TryGetValue(kvp.Key, out GameObject tag))
            {
                UpdateTagPosition(tag, kvp.Value.position);
            }
        }
    }

    public void AddAlly(int id, Transform targetTransform)
    {
        if (allyObjects.ContainsKey(id)) return;

        GameObject allyTag = Instantiate(allyTagPrefab, transform);
        allyObjects.Add(id, allyTag);
        targetTransforms[id] = targetTransform;
        UpdateTagPosition(allyTag, targetTransform.position);
    }

    public void AddEnemy(int id, Transform targetTransform)
    {
        if (allyObjects.ContainsKey(id)) return;

        GameObject enemyTag = Instantiate(enemyTagPrefab, transform);
        allyObjects.Add(id, enemyTag);
        targetTransforms[id] = targetTransform;
        UpdateTagPosition(enemyTag, targetTransform.position);
    }

    private void UpdateTagPosition(GameObject tag, Vector3 worldPosition)
    {
        worldPosition.y += 2f; // 将标签放在目标上方
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPos.z > 0)
        {
            tag.transform.position = new Vector3(screenPos.x, screenPos.y, 0);
            tag.SetActive(true);
        }
        else
        {
            tag.SetActive(false);
        }
    }

    public void RemoveAlly(int id)
    {
        if (allyObjects.TryGetValue(id, out GameObject obj))
        {
            Destroy(obj);
            allyObjects.Remove(id);
            targetTransforms.Remove(id);
        }
    }

    public void RemoveEnemy(int id)
    {
        RemoveAlly(id); // 使用相同的移除逻辑
    }
}

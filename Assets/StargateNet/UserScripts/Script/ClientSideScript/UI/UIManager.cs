using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField]
    private AssetsPathTable assetsPathTable;
    private static UIManager _instance;
    private Canvas canvas;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UIManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("UIManager");
                    _instance = go.AddComponent<UIManager>();
                }
            }
            return _instance;
        }
    }

    private Dictionary<Type, UIBase> uiDictinary = new Dictionary<Type, UIBase>();

    private bool _isInitialized = false;
    public bool IsInitialized => _isInitialized;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 获取或创建 Canvas
        canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }

    private async void Start()
    {
        await LoadAllUI();
        _isInitialized = true;
        Debug.Log("所有UI加载完成");
    }

    private async Task LoadAllUI()
    {
        if (assetsPathTable == null)
        {
            Debug.LogError("AssetsPathTable 未设置");
            return;
        }

        var loadTasks = new List<Task>();
        foreach (var uiName in assetsPathTable.uiPaths)
        {
            loadTasks.Add(LoadUI(uiName));
        }
        
        await Task.WhenAll(loadTasks);
    }

    private async Task LoadUI(string prefabName)
    {
        try
        {
            string addressableKey = prefabName;
            var operation = Addressables.LoadAssetAsync<GameObject>(addressableKey);
            var prefab = await operation.Task;
            
            if (prefab != null)
            {
                // 实例化到 Canvas 下
                var uiObject = Instantiate(prefab, canvas.transform);
                var uiComponent = uiObject.GetComponent<UIBase>();
                
                if (uiComponent != null)
                {
                    var type = uiComponent.GetType();
                    uiDictinary.Add(type, uiComponent);
                    uiComponent.Init();
                    Debug.Log($"加载UI成功: {prefabName}");
                }
                else
                {
                    Debug.LogError($"预制体 {prefabName} 上没有找到 UIBase 组件");
                    Destroy(uiObject);
                }
            }
            else
            {
                Debug.LogError($"无法加载UI预制体: {prefabName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载UI失败 {prefabName}: {e.Message}");
        }
    }

    public T GetUIPanel<T>() where T : UIBase
    {
        Type type = typeof(T);
        return uiDictinary.TryGetValue(type, out UIBase panel) ? panel as T : null;
    }

    public void ShowUI<T>() where T : UIBase
    {
        if (!_isInitialized)
        {
            Debug.LogWarning($"UI管理器尚未初始化完成，无法显示 {typeof(T).Name}");
            return;
        }

        var panel = GetUIPanel<T>();
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"未找到UI面板: {typeof(T).Name}");
        }
    }

    public void HideUI<T>() where T : UIBase
    {
        var panel = GetUIPanel<T>();
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        foreach (var ui in uiDictinary.Values)
        {
            if (ui != null)
            {
                Addressables.ReleaseInstance(ui.gameObject);
            }
        }
        uiDictinary.Clear();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public class BotSceneLoader : MonoBehaviour 
{
    [SerializeField] private int botSceneCount = 30;
    [SerializeField] private string sceneName = "MainScene";
    [SerializeField] private float loadInterval = 0.1f;
    [SerializeField] private float startDelay = 10f;  // 添加启动延迟时间
    
    private List<StargateNet.GameStarter> _botStarters = new List<StargateNet.GameStarter>();


    public void StartLoadBotScenes()
    {
        StartCoroutine(LoadBotScenes());
    }

    private IEnumerator LoadBotScenes()
    {
        for (int i = 0; i < botSceneCount; i++)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            while (!operation.isDone)
            {
                yield return null;
            }
            
            Scene botScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            GameObject[] rootObjects = botScene.GetRootGameObjects();
            
            // 禁用场景组件
            DisableBotSceneComponents(rootObjects);
            
            #if UNITY_EDITOR
            foreach (var obj in rootObjects)
            {
                SceneVisibilityManager.instance.Hide(obj, true);
            }
            #endif
            
            string newName = $"{sceneName}_{i}";
            Debug.Log($"Loaded bot scene: {newName}");
            
            // 收集所有GameStarter而不是立即启动
            foreach (var obj in rootObjects)
            {
                if (obj.TryGetComponent<StargateNet.GameStarter>(out var gameStarter))
                {
                    gameStarter.IsBotScene = true;
                    _botStarters.Add(gameStarter);
                    break;
                }
            }
            
            yield return new WaitForSeconds(loadInterval);
        }
        
        Debug.Log($"Finished loading {botSceneCount} bot scenes, waiting {startDelay} seconds to start...");
        
        // 等待指定时间
        yield return new WaitForSeconds(startDelay);
        
        // 启动所有bot客户端
        foreach (var starter in _botStarters)
        {
            starter.StartBotClient();
            yield return new WaitForSeconds(0.1f); // 每个bot之间稍微间隔一下
        }
        
        Debug.Log("All bots started!");
    }
    
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), 
            $"Loaded Scenes: {SceneManager.sceneCount}, Bots: {_botStarters.Count}");
    }
    
    private void DisableBotSceneComponents(GameObject[] rootObjects)
    {
        foreach (var obj in rootObjects)
        {
            // 禁用相机
            var cameras = obj.GetComponentsInChildren<Camera>(true);
            foreach (var camera in cameras)
            {
                camera.enabled = false;
            }

            // 禁用音频监听器
            var audioListeners = obj.GetComponentsInChildren<AudioListener>(true);
            foreach (var listener in audioListeners)
            {
                listener.enabled = false;
            }

            // 禁用输入系统
            var inputHandlers = obj.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
            foreach (var handler in inputHandlers)
            {
                handler.enabled = false;
            }

            // 禁用UI Canvas
            var canvases = obj.GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                canvas.enabled = false;
            }

            // 禁用光源
            var lights = obj.GetComponentsInChildren<Light>(true);
            foreach (var light in lights)
            {
                light.enabled = false;
            }
        }
    }
}

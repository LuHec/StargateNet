using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class UIBase : MonoBehaviour
{
    protected CanvasGroup uiPanel;
    private Coroutine autoCloseCoroutine;

    public void Init()
    {
        uiPanel = GetComponent<CanvasGroup>();
        if (uiPanel == null)
        {
            uiPanel = gameObject.AddComponent<CanvasGroup>();
        }
        OnInit();
        CloseWithoutCallback();
    }

    private void CloseWithoutCallback()
    {
        uiPanel.alpha = 0;
        uiPanel.interactable = false;
        uiPanel.blocksRaycasts = false;
    }

    public void Open()
    {
        // 停止自动关闭协程
        StopAutoCloseCoroutine();

        uiPanel.alpha = 1;
        uiPanel.interactable = true;
        uiPanel.blocksRaycasts = true;
        OnOpen();
    }

    public void Close()
    {
        // 停止自动关闭协程
        StopAutoCloseCoroutine();

        uiPanel.alpha = 0;
        uiPanel.interactable = false;
        uiPanel.blocksRaycasts = false;
        OnClose();
    }

    /// <summary>
    /// 打开UI并在指定时间后自动关闭
    /// </summary>
    /// <param name="duration">持续时间(秒)</param>
    public void OpenWithDuration(float duration)
    {
        // 如果已经有自动关闭的协程在运行，先停止它
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
        }

        // 打开UI
        Open();

        // 启动自动关闭协程
        autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(duration));
    }

    private IEnumerator AutoCloseAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        Close();
        autoCloseCoroutine = null;
    }

    // 在OnDestroy中确保协程被正确清理
    protected virtual void OnDestroy()
    {
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
    }

    // 抽取停止协程的逻辑为单独的方法
    private void StopAutoCloseCoroutine()
    {
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
    }

    protected virtual void OnInit() { }
    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
}

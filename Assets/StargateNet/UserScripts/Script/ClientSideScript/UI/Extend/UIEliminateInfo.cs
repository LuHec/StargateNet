using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class UIEliminateInfo : UIBase
{
    [Header("Message Settings")]
    [SerializeField] private RectTransform messageRect;  // 直接引用消息UI对象
    [SerializeField] private float minShowDuration = 1f; // 最短显示时间
    [SerializeField] private float maxShowDuration = 3f; // 最大显示时间（队列为空时）

    [Header("Animation Settings")]
    [SerializeField] private float slideInDuration = 0.3f;
    [SerializeField] private float slideOutDuration = 0.2f;
    [SerializeField] private float messageOffsetX = 400f;
    [SerializeField] private Ease slideInEase = Ease.OutBack;
    [SerializeField] private Ease slideOutEase = Ease.InBack;

    private Queue<int> pendingMessages = new Queue<int>();
    private bool isShowingMessage = false;
    private float currentMessageShowTime;
    private Sequence currentSequence;
    private bool isAnimating = false;

    protected override void OnInit()
    {
        base.OnInit();
        messageRect.anchoredPosition = new Vector2(-messageOffsetX, messageRect.anchoredPosition.y);
    }

    private void Update()
    {
        if (isAnimating) return;

        if (!isShowingMessage && pendingMessages.Count > 0)
        {
            // 显示新消息
            ShowNextMessage();
        }
        else if (isShowingMessage)
        {
            float currentTime = Time.time - currentMessageShowTime;
            bool hasMoreMessages = pendingMessages.Count > 0;
            
            // 根据队列状态决定显示时间
            float targetDuration = hasMoreMessages ? minShowDuration : maxShowDuration;
            
            if (currentTime >= targetDuration)
            {
                HideCurrentMessage();
            }
        }
    }

    public void AddMessage(int killerId)
    {
        pendingMessages.Enqueue(killerId);
    }

    private void ShowNextMessage()
    {
        int killerId = pendingMessages.Dequeue();
        isShowingMessage = true;
        isAnimating = true;
        currentMessageShowTime = Time.time;

        // 更新消息内容（根据实际需求修改）
        // messageRect.GetComponentInChildren<TextMeshProUGUI>().text = $"Player {killerId} eliminated!";

        // 创建滑入动画
        currentSequence?.Kill();
        currentSequence = DOTween.Sequence();
        currentSequence.Append(messageRect.DOAnchorPosX(0, slideInDuration).SetEase(slideInEase));
        currentSequence.OnComplete(() => isAnimating = false);
    }

    private void HideCurrentMessage()
    {
        isShowingMessage = false;
        isAnimating = true;

        currentSequence?.Kill();
        currentSequence = DOTween.Sequence();
        currentSequence.Append(messageRect.DOAnchorPosX(-messageOffsetX, slideOutDuration).SetEase(slideOutEase));
        currentSequence.OnComplete(() => isAnimating = false);
    }

    private void OnHideComplete()
    {
        isShowingMessage = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        currentSequence?.Kill();
        pendingMessages.Clear();
    }
}

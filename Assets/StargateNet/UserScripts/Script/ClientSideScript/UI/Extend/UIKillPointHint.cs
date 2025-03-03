using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;

public class UIKillPointHint : UIBase
{
    public TMP_Text ptText;
    public TMP_Text killText;

    private Sequence ptSequence;
    private Sequence killSequence;

    protected override void OnInit()
    {
        // 初始化时设置文本为0缩放
        ptText.transform.localScale = Vector3.zero;
        killText.transform.localScale = Vector3.zero;
    }

    public void PlayKillAnimation(string killInfo, int points)
    {
        // 关闭之前的动画
        ptSequence?.Kill();
        killSequence?.Kill();

        // 设置文本内容
        ptText.text = $"+{points}";
        killText.text = killInfo;

        // 重置状态
        ptText.transform.localScale = Vector3.zero;
        killText.transform.localScale = Vector3.zero;
        ptText.alpha = 0;
        killText.alpha = 0;

        // 创建pt文本动画序列
        ptSequence = DOTween.Sequence()
            .Join(ptText.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack))
            .Join(ptText.DOFade(1, 0.2f))
            .Append(ptText.transform.DOScale(1f, 0.1f))
            .AppendInterval(0.5f)
            .Append(ptText.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack))
            .Join(ptText.DOFade(0, 0.2f));

        // 创建kill文本动画序列（稍微延迟开始）
        killSequence = DOTween.Sequence()
            .SetDelay(0.3f)
            .Join(killText.transform.DOScale(1.2f, 0.5f).SetEase(Ease.OutBack))
            .Join(killText.DOFade(1, 0.2f))
            .Append(killText.transform.DOScale(1f, 0.1f))
            .AppendInterval(0.5f)
            .Append(killText.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack))
            .Join(killText.DOFade(0, 0.2f));

        // 打开UI并设置自动关闭时间
        OpenWithDuration(1.8f);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 确保动画在销毁时被清理
        ptSequence?.Kill();
        killSequence?.Kill();
    }
}
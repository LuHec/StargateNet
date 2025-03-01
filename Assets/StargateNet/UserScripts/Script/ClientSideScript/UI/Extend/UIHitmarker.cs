using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIHitmarker : UIBase
{
    [Header("Random Offset")]
    [SerializeField] private float maxRotation = 45f;
    [SerializeField] private float maxOffset = 10f;
    [SerializeField]
    private float duration = 0.1f;

    private RectTransform _markerTransform;

    protected override void OnInit()
    {
        _markerTransform = transform as RectTransform;
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        // 生成随机旋转和位移
        float randomRotation = Random.Range(-maxRotation, maxRotation);
        Vector2 randomOffset = Random.insideUnitCircle * maxOffset;

        _markerTransform.anchoredPosition = randomOffset;
        _markerTransform.localRotation = Quaternion.Euler(0, 0, randomRotation);
    }

    protected override void OnClose()
    {
        base.OnClose();
        // 重置位置和旋转
        _markerTransform.anchoredPosition = Vector2.zero;
        _markerTransform.localRotation = Quaternion.identity;
    }

    public void HitToShowMarker()
    {
        this.OpenWithDuration(duration);
    }
}

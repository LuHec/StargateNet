using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIBattleInterface : UIBase
{
    public TMP_Text aPoint;
    public TMP_Text bPoint;
    public TMP_Text battleTime;
    public TMP_Text controllPointPercent;
    public Image flag;
    public Sprite aFlag;
    public Sprite bFlag;

    protected override void OnInit()
    {
        HideControlPoint();
    }

    public void ShowControlPoint(int i)
    {
        if(i == 0)
        {
            flag.sprite = aFlag;
        }
        else
        {
            flag.sprite = bFlag;
        }
        flag.gameObject.SetActive(true);
    }

    public void HideControlPoint()
    {
     
        flag.gameObject.SetActive(false);
    }

    public void SetAPoint(int point)
    {
        aPoint.text = point.ToString();
    }

    public void SetBPoint(int point)
    {
        bPoint.text = point.ToString();
    }

    // 删除原有的 SetSecond 和 SetMinute 方法
    // 添加新的时间设置方法
    public void SetTime(int minutes, int seconds)
    {
        battleTime.text = $"{minutes:D2}:{seconds:D2}";
    }

    // 如果需要从总秒数设置时间
    public void SetTimeFromSeconds(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        SetTime(minutes, seconds);
    }

    public void SetPercent(float percent)
    {
        // percent取整
        controllPointPercent.text = ((int)percent).ToString();
    }
}

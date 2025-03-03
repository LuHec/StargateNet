using TMPro;
using UnityEngine;
using System.Text;

public class UIPlayerInterface : UIBase
{
    [Header("Progress Bars")]
    public ProgressBar hpBar;
    public ProgressBar armorBar;
    public ProgressBar magBar;

    [Header("Bar Settings")]
    [SerializeField] private Color hpColor = Color.green;
    [SerializeField] private Color hpAlertColor = Color.red;
    [SerializeField] private Color armorColor = Color.blue;
    [SerializeField] private Color magColor = Color.yellow;

    [Header("Alert Settings")]
    [SerializeField] private int hpAlertThreshold = 30;
    [SerializeField] private int armorAlertThreshold = 20;
    [SerializeField] private AudioClip lowHpSound;

    protected override void OnInit()
    {
        // 初始化血条设置
        InitializeBar(hpBar, "HP", hpColor, hpAlertColor, 100, hpAlertThreshold, lowHpSound);
        
        // 初始化护甲条设置
        InitializeBar(armorBar, "ARMOR", armorColor, Color.grey, 100, armorAlertThreshold);
        
        // 初始化弹药条设置
        InitializeBar(magBar, "MAG", magColor, Color.grey, 30, 0);
    }

    private void InitializeBar(ProgressBar bar, string title, Color normalColor, Color alertColor, 
        int maxValue, int alertThreshold, AudioClip alertSound = null)
    {
        if (bar == null) return;
        
        // bar.Title = title;
        // bar.TitleColor = Color.white;
        // bar.TitleFontSize = 14;
        // bar.MaxValue = maxValue;
        // bar.BarColor = normalColor;
        // bar.BarAlertColor = alertColor;
        // bar.Alert = alertThreshold;
        // bar.sound = alertSound;
        // bar.repeat = alertSound != null;
        // bar.RepeatRate = 1f;
        
        // // 设置初始值
        // bar.BarValue = maxValue;
    }

    public void UpdateHP(int hp)
    {
        if (hpBar != null)
        {
            hpBar.BarValue = hp;
        }
    }

    public void UpdateArmor(int armor)
    {
        if (armorBar != null)
        {
            armorBar.BarValue = armor;
        }
    }

    public void UpdateMag(int mag)
    {
        if (magBar != null)
        {
            magBar.BarValue = mag;
        }
    }

    protected override void OnOpen()
    {
        // 确保打开时所有进度条都可见
        if (hpBar) hpBar.gameObject.SetActive(true);
        if (armorBar) armorBar.gameObject.SetActive(true);
        if (magBar) magBar.gameObject.SetActive(true);
    }

    protected override void OnClose()
    {
        // 关闭时隐藏所有进度条
        if (hpBar) hpBar.gameObject.SetActive(false);
        if (armorBar) armorBar.gameObject.SetActive(false);
        if (magBar) magBar.gameObject.SetActive(false);
    }
}

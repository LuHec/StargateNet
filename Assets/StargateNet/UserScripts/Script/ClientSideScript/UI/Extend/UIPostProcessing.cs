using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPostProcessing : UIBase
{
    public Sprite damagedTexture;
    private Image image;

    protected override void OnInit()
    {
        image = GetComponent<Image>();
        image.sprite = damagedTexture;
    }

    protected override void OnOpen()
    {
        
    }

    protected override void OnClose()
    {
       
    }

}

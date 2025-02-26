using TMPro;

public class UIPlayerInterface : UIBase
{
    public TMP_Text HP;
    public TMP_Text Armor;
    public TMP_Text Mag;
    
    // 使用静态字符串缓存避免GC
    private static readonly string HP_FORMAT = "{0}";
    private static readonly string ARMOR_FORMAT = "{0}";
    private static readonly string Mag_FORMAT = "{0}";
    
    // 使用 StringBuilder 来避免字符串拼接的垃圾回收
    private readonly System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(32);

    protected override void OnInit()
    {
        HP.text = string.Format(HP_FORMAT, 100);
        Armor.text = string.Format(ARMOR_FORMAT, 100);
        Mag.text = string.Format(Mag_FORMAT, 31);
    }

    protected override void OnOpen()
    {

    }

    protected override void OnClose()
    {

    }

    public void UpdateHP(int hp)
    {
        stringBuilder.Length = 0;
        stringBuilder.AppendFormat(HP_FORMAT, hp);
        HP.text = stringBuilder.ToString();
    }

    public void UpdateArmor(int armor)
    {
        stringBuilder.Length = 0;
        stringBuilder.AppendFormat(ARMOR_FORMAT, armor);
        Armor.text = stringBuilder.ToString();
    }

    public void UpdateMag(int mag)
    {
        stringBuilder.Length = 0;
        stringBuilder.AppendFormat(Mag_FORMAT, mag);
        Mag.text = stringBuilder.ToString();
    }
}

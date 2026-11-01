using UnityEngine;
using UnityEngine.UI;

/// <summary>A row of heart images. Hearts that are lost are darkened.</summary>
public class HeartBar : MonoBehaviour
{
    public Image[] hearts;
    public Color fullColor = Color.white;
    public Color lostColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    /// <summary>The first 'count' hearts are shown as full, the rest as lost.</summary>
    public void Set(int count)
    {
        if (hearts == null) return;
        for (int i = 0; i < hearts.Length; i++)
            if (hearts[i] != null) hearts[i].color = i < count ? fullColor : lostColor;
    }
}

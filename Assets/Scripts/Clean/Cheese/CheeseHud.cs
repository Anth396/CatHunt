using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cheese game's part of the HUD: three cheese icons that go dark as cheese is lost, three rat icons that
/// light up as rats are caught, and the win / lose text on the end popup. The score text, the popup and the
/// pause menu are the same ones the other levels use (HudView, PauseController).
/// </summary>
public class CheeseHud : MonoBehaviour
{
    public CheeseGame game;
    public IntVariable gameScore;

    [Header("Icons")]
    public Image[] cheeseIcons;
    public Image[] ratIcons;
    public Color cheeseKeptColor = Color.white;
    public Color cheeseLostColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color ratFreeColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color ratCaughtColor = Color.white;

    [Header("End popup")]
    public GameObject endPopup;
    public TextMeshProUGUI endText;
    [Tooltip("Extra line under the result (what to do next).")]
    public string endHint = "Enter the glowing rat hole to continue";

    void Start()
    {
        if (game == null) game = FindFirstObjectByType<CheeseGame>();
        Refresh();
    }

    // Wired to the game's events: a rat was caught, a cheese was lost, the game was restarted
    public void Refresh()
    {
        if (game == null) return;
        Paint(cheeseIcons, game.CheeseLost, cheeseLostColor, cheeseKeptColor);
        Paint(ratIcons, game.RatsCaught, ratCaughtColor, ratFreeColor);
    }

    // The first 'count' icons get 'doneColor', the rest 'otherColor'
    private static void Paint(Image[] icons, int count, Color doneColor, Color otherColor)
    {
        if (icons == null) return;
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) icons[i].color = i < count ? doneColor : otherColor;
    }

    public void ShowWin()
    {
        ShowEnd("YOU WIN!");
    }

    public void ShowLose()
    {
        ShowEnd("YOU LOSE");
    }

    private void ShowEnd(string title)
    {
        if (endText != null)
        {
            endText.text = title + "\nYour Score: " + (gameScore != null ? gameScore.Value : 0);
            if (!string.IsNullOrEmpty(endHint)) endText.text += "\n" + endHint;
        }
        if (endPopup != null) endPopup.SetActive(true);
    }
}

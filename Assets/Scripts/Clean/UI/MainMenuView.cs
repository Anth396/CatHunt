using TMPro;
using UnityEngine;

/// <summary>Main menu: shows the saved high score and starts a new game from the Start button.</summary>
public class MainMenuView : MonoBehaviour
{
    public TextMeshProUGUI highScoreText;
    public IntVariable gameScore;
    public StringGameEvent sceneRequested;
    public string firstLevel = "city";

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (highScoreText != null) highScoreText.text = "High Score - " + HighScoreKeeper.SavedHighScore;
    }

    // Start button
    public void StartNewGame()
    {
        if (gameScore != null) gameScore.SetValue(0);
        if (sceneRequested != null) sceneRequested.Raise(firstLevel);
    }
}

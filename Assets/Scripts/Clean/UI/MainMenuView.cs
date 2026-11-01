using TMPro;
using UnityEngine;

/// <summary>Main menu: shows the saved high score and starts a new game (the lobby) from the Start button.</summary>
public class MainMenuView : MonoBehaviour
{
    public TextMeshProUGUI highScoreText;
    public IntVariable gameScore;
    public StringGameEvent sceneRequested;
    [Tooltip("Scene the Start button goes to (through the loading scene). It must be in the Build Settings.")]
    public string firstLevel = "lobby";

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

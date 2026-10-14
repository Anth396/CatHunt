using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu scene: shows the saved high score and starts a new game from the Start button.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public TextMeshProUGUI scoreText;
    public string firstLevelScene = "city";

    void Start()
    {
        Time.timeScale = 1f;
        if (scoreText != null) scoreText.text = "High Score - " + GameManager.SavedHighScore;
    }

    // Hook this up to the Start button
    public void StartGame()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartNewGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(firstLevelScene);
    }
}

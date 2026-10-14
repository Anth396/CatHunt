using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pause popup. Hook the buttons up in the Inspector:
/// Pause button -> Pause, Resume -> Resume, Restart -> Restart, Main Menu -> GoToMainMenu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public string mainMenuScene = "mainmenu";

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void Pause()
    {
        // Ignore it once time is up, the game over popup owns the screen then
        GameManager gm = GameManager.Instance;
        if (gm != null && !gm.IsRunning) return;

        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // Resets the score, timer, player and rats (GameManager also restores timeScale)
    public void Restart()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        else Time.timeScale = 1f;
    }

    public void GoToMainMenu()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null) gm.SaveHighScore();

        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }
}

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
        AudioListener.pause = false;    // a global flag, so make sure a new level never starts muted
    }

    public void Pause()
    {
        // Ignore it once time is up, the game over popup owns the screen then
        GameManager gm = GameManager.Instance;
        if (gm != null && !gm.IsRunning) return;

        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;     // freezes every playing sound
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;    // sounds continue where they stopped
    }

    // Resets the score, timer, player and rats (GameManager also restores timeScale)
    public void Restart()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        AudioListener.pause = false;
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        else Time.timeScale = 1f;
    }

    public void GoToMainMenu()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null) gm.EndSession();    // stops the countdown and saves the high score

        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuScene);
    }
}

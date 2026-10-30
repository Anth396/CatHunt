using UnityEngine;

/// <summary>
/// Pause popup. The pause and resume buttons call Pause / Resume; the Restart and Main Menu buttons
/// raise events directly from their OnClick (no reference to the scripts that react).
/// </summary>
public class PauseController : MonoBehaviour
{
    public GameObject pausePanel;
    public BoolVariable gameRunning;

    void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void Pause()
    {
        // Once time is up the time's up popup owns the screen
        if (gameRunning != null && !gameRunning.Value) return;

        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;     // freezes every playing sound
    }

    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    // GameRestart / MainMenuRequested response: never leave time or audio frozen
    public void ClearPause()
    {
        Resume();
    }
}

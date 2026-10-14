using UnityEngine;
using TMPro;

/// <summary>
/// All UI: subscribes to the GameManager events and updates the timer, score and game over popup.
/// </summary>
public class HUDManager : Singleton<HUDManager>
{
    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public GameObject popUpPanel;
    public TextMeshProUGUI finalScoreText;

    private GameManager gm;

    // Bound to this scene's GameManager and UI, so each scene uses its own HUD
    protected override bool Persistent => false;

    void Start()
    {
        gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("HUDManager: no GameManager in the scene.", this);
            return;
        }

        gm.scoreChange.AddListener(OnScoreChange);
        gm.timeChange.AddListener(OnTimeChange);
        gm.gameStart.AddListener(OnGameStart);
        gm.gameRestart.AddListener(OnGameRestart);
        gm.gameOver.AddListener(OnGameOver);

        // Show the current values right away
        OnScoreChange(gm.Score);
        OnTimeChange(gm.startTimeSeconds);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (gm == null) return;
        gm.scoreChange.RemoveListener(OnScoreChange);
        gm.timeChange.RemoveListener(OnTimeChange);
        gm.gameStart.RemoveListener(OnGameStart);
        gm.gameRestart.RemoveListener(OnGameRestart);
        gm.gameOver.RemoveListener(OnGameOver);
    }

    private void OnScoreChange(int score)
    {
        if (scoreText != null) scoreText.text = "Score - " + score;
    }

    private void OnTimeChange(int secondsLeft)
    {
        if (timerText == null) return;
        int t = Mathf.Max(secondsLeft, 0);
        timerText.text = string.Format("Timer - {0:00}:{1:00}", t / 60, t % 60);
    }

    private void OnGameStart()
    {
        if (popUpPanel != null) popUpPanel.SetActive(false);
    }

    private void OnGameRestart()
    {
        if (popUpPanel != null) popUpPanel.SetActive(false);
    }

    private void OnGameOver()
    {
        if (finalScoreText != null) finalScoreText.text = "Time's Up!\nYour Score: " + gm.Score;
        if (popUpPanel != null) popUpPanel.SetActive(true);
    }
}

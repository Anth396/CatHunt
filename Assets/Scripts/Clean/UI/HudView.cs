using TMPro;
using UnityEngine;

/// <summary>On-screen score, timer and the time's-up popup. Everything arrives through event listeners.</summary>
public class HudView : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public GameObject popUpPanel;
    public TextMeshProUGUI finalScoreText;
    public IntVariable gameScore;

    // ScoreChanged response
    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = "Score - " + score;
    }

    // TimeChanged response
    public void SetTime(int secondsLeft)
    {
        if (timerText == null) return;
        int t = Mathf.Max(secondsLeft, 0);
        timerText.text = string.Format("Timer - {0:00}:{1:00}", t / 60, t % 60);
    }

    // GameOver response
    public void ShowGameOver()
    {
        if (finalScoreText != null && gameScore != null)
        {
            finalScoreText.text = "Time's Up!\nYour Score: " + gameScore.Value;
        }
        if (popUpPanel != null) popUpPanel.SetActive(true);
    }

    // GameStart / GameRestart response
    public void HidePopup()
    {
        if (popUpPanel != null) popUpPanel.SetActive(false);
    }
}

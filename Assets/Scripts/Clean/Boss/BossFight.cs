using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The boss fight of the cellar. Keeps the heart bars up to date, awards the points for defeating the boss, and
/// ends the fight: the boss out of hearts is a win, the cat out of hearts is a loss. GameRestart starts it over.
/// </summary>
public class BossFight : MonoBehaviour
{
    [Header("Fighters")]
    public RatBoss boss;
    public CatHealth catHealth;
    public HeartBar bossHearts;
    public HeartBar catHearts;

    [Header("Score")]
    public int defeatPoints = 400;
    public IntVariable gameScore;
    public IntGameEvent scoreChanged;
    public BoolVariable gameRunning;
    [Tooltip("Off: the score from the lobby carries into the fight. On: start from zero (e.g. testing the cellar alone).")]
    public bool resetScoreOnStart = false;

    [Header("End of the fight")]
    [Tooltip("Seconds between the boss falling over and the win popup.")]
    public float winDelay = 2f;
    public float loseDelay = 0.8f;
    public GameObject endPopup;
    public TextMeshProUGUI endText;
    public UnityEvent onWin = new UnityEvent();
    public UnityEvent onLose = new UnityEvent();

    public bool Ended { get; private set; }

    private int startScore;

    void Start()
    {
        if (boss != null)
        {
            boss.onHeartsChanged.AddListener(RefreshHearts);
            boss.onDefeated.AddListener(OnBossDefeated);
        }
        if (catHealth != null)
        {
            catHealth.onChanged.AddListener(RefreshHearts);
            catHealth.onDied.AddListener(OnCatDied);
        }

        // The score of the lobby carries over; a restart of the fight returns to it
        if (gameScore != null)
        {
            if (resetScoreOnStart) gameScore.SetValue(0);
            startScore = gameScore.Value;
        }
        Publish();
        if (gameRunning != null) gameRunning.SetValue(true);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        RefreshHearts();
    }

    void OnDestroy()
    {
        if (boss != null)
        {
            boss.onHeartsChanged.RemoveListener(RefreshHearts);
            boss.onDefeated.RemoveListener(OnBossDefeated);
        }
        if (catHealth != null)
        {
            catHealth.onChanged.RemoveListener(RefreshHearts);
            catHealth.onDied.RemoveListener(OnCatDied);
        }
    }

    public void RefreshHearts()
    {
        if (bossHearts != null && boss != null) bossHearts.Set(boss.Hearts);
        if (catHearts != null && catHealth != null) catHearts.Set(catHealth.Hearts);
    }

    private void OnBossDefeated()
    {
        if (Ended) return;
        Add(defeatPoints);
        StartCoroutine(EndAfter(true, winDelay));
    }

    private void OnCatDied()
    {
        if (Ended) return;
        StartCoroutine(EndAfter(false, loseDelay));
    }

    private IEnumerator EndAfter(bool won, float seconds)
    {
        Ended = true;           // the other ending can no longer happen
        yield return new WaitForSeconds(seconds);

        if (gameRunning != null) gameRunning.SetValue(false);
        Time.timeScale = 0f;
        if (endText != null) endText.text = (won ? "YOU WIN!" : "YOU LOSE") + "\nYour Score: " + (gameScore != null ? gameScore.Value : 0);
        if (endPopup != null) endPopup.SetActive(true);
        (won ? onWin : onLose).Invoke();
    }

    private void Add(int points)
    {
        if (gameScore == null) return;
        gameScore.ApplyChange(points);
        Publish();
    }

    private void Publish()
    {
        if (scoreChanged != null && gameScore != null) scoreChanged.Raise(gameScore.Value);
    }

    // GameRestart response
    public void ResetState()
    {
        StopAllCoroutines();
        Ended = false;
        if (gameScore != null) gameScore.SetValue(startScore);
        Publish();
        if (gameRunning != null) gameRunning.SetValue(true);
        Time.timeScale = 1f;
        if (boss != null) boss.ResetState();
        if (catHealth != null) catHealth.ResetState();
        RefreshHearts();
    }
}

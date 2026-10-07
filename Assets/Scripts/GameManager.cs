using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // Singleton Instance
    public static GameManager Instance { get; private set; }

    [Header("UI & References")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public GameObject popUpPanel;
    public TextMeshProUGUI finalScoreText;
    public GameObject enemies;
    public Transform playerTransform;
    public Rigidbody2D playerBody;

    [Header("Game Settings")]
    public int startTimeSeconds = 120;      // countdown start (2 minutes)
    public int scorePerCatch = 100;

    // Time left on the countdown (seconds) and current score
    [System.NonSerialized] public int timeLeft = 0;
    [System.NonSerialized] public int score = 0;

    private bool gameRunning = false;
    private bool resettingAfterCatch = false;
    private Coroutine timerRoutine;

    // True while the player is in the slash state (needed to catch the prey)
    public bool IsPlayerSlashing
    {
        get
        {
            if (playerBody == null) return false;
            PlayerMovement p = playerBody.GetComponent<PlayerMovement>();
            return p != null && p.IsSlashing;
        }
    }

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        Application.targetFrameRate = 30;
        score = 0;
        UpdateScoreText();
        StartTimer();
    }

    // ---------------------------------------------------------------- timer

    public void StartTimer()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }
        timeLeft = startTimeSeconds;
        gameRunning = true;
        timerRoutine = StartCoroutine(TimerTick());
    }

    private IEnumerator TimerTick()
    {
        while (gameRunning)
        {
            UpdateTimerText();
            if (timeLeft <= 0)
            {
                TimeUp();
                yield break;
            }
            yield return new WaitForSeconds(1f);
            timeLeft--;
        }
    }

    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            int t = Mathf.Max(timeLeft, 0);
            timerText.text = string.Format("Timer - {0:00}:{1:00}", t / 60, t % 60);
        }
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score - " + score;
        }
    }

    // ---------------------------------------------------------------- catching / game over

    // Called when the slashing cat touches a rat: +score, then the cat and the rats are reset
    public void PreyCaught()
    {
        if (!gameRunning || resettingAfterCatch) return;

        score += scorePerCatch;
        UpdateScoreText();
        Debug.Log("Prey caught! Score: " + score);

        // Reset after the current physics callback has finished
        resettingAfterCatch = true;
        StartCoroutine(ResetAfterCatch());
    }

    private IEnumerator ResetAfterCatch()
    {
        yield return null;
        ResetPlayerAndRats();
        resettingAfterCatch = false;
    }

    private void TimeUp()
    {
        gameRunning = false;
        Time.timeScale = 0.0f;

        if (popUpPanel != null)
        {
            if (finalScoreText != null)
            {
                finalScoreText.text = "Time's Up!\nYour Score: " + score;
            }
            popUpPanel.SetActive(true);
        }
    }

    // Hook this up to your UI Restart Button
    public void RestartButtonCallback(int input)
    {
        Debug.Log("Restart!");
        ResetGame();
        Time.timeScale = 1.0f;
    }

    // Player + rats only (what happens after each catch)
    private void ResetPlayerAndRats()
    {
        if (playerBody != null)
        {
            PlayerMovement player = playerBody.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.ResetState();
            }
            else
            {
                playerBody.linearVelocity = Vector2.zero;
            }
        }

        foreach (EnemyMovement enemyMove in FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None))
        {
            enemyMove.ResetState();
        }
    }

    // Full restart: score, timer, popup, player, rats and breakable objects
    private void ResetGame()
    {
        StopAllCoroutines();
        resettingAfterCatch = false;

        score = 0;
        UpdateScoreText();

        if (popUpPanel != null)
        {
            popUpPanel.SetActive(false);
        }

        ResetPlayerAndRats();

        foreach (BreakableObject breakable in FindObjectsByType<BreakableObject>(FindObjectsSortMode.None))
        {
            breakable.ResetState();
        }

        StartTimer();
    }
}

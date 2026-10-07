using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns the game state (score, countdown) and announces changes through UnityEvents.
/// UI lives in HUDManager, enemy resets in EnemyManager; neither is referenced from here.
/// </summary>
public class GameManager : MonoBehaviour
{
    // Singleton Instance
    public static GameManager Instance { get; private set; }

    [Header("Events")]
    public UnityEvent gameStart;            // the countdown has begun
    public UnityEvent gameRestart;          // full restart (score, timer, everything)
    public UnityEvent<int> scoreChange;     // new score
    public UnityEvent gameOver;             // time ran out
    public UnityEvent<int> timeChange;      // seconds left, once per second
    public UnityEvent roundReset;           // after a catch: player and rats go back to start

    [Header("References")]
    public Rigidbody2D playerBody;

    [Header("Game Settings")]
    public int startTimeSeconds = 120;      // countdown start (2 minutes)
    public int scorePerCatch = 100;

    public int Score { get; private set; }
    public int TimeLeft { get; private set; }

    private bool gameRunning = false;
    private bool resettingAfterCatch = false;
    private Coroutine timerRoutine;

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

        // Listen to the things that can happen in the world
        PlayerMovement player = playerBody != null ? playerBody.GetComponent<PlayerMovement>() : null;
        if (player != null)
        {
            player.restartRequested.AddListener(RestartGame);
        }
        foreach (CatchThePrey catcher in FindObjectsByType<CatchThePrey>(FindObjectsSortMode.None))
        {
            catcher.preyCaught.AddListener(PreyCaught);
        }

        // One frame later, so every listener (HUD, enemies...) has subscribed in its own Start
        StartCoroutine(StartGameNextFrame());
    }

    private IEnumerator StartGameNextFrame()
    {
        yield return null;
        StartGame();
    }

    // ---------------------------------------------------------------- flow

    private void StartGame()
    {
        Score = 0;
        scoreChange.Invoke(Score);
        StartTimer();
        gameStart.Invoke();
    }

    // Hook this up to a UI Restart Button
    public void RestartButtonCallback(int input)
    {
        RestartGame();
    }

    public void RestartGame()
    {
        Debug.Log("Restart!");
        StopAllCoroutines();
        resettingAfterCatch = false;
        Time.timeScale = 1.0f;

        Score = 0;
        ResetPlayer();

        foreach (BreakableObject breakable in FindObjectsByType<BreakableObject>(FindObjectsSortMode.None))
        {
            breakable.ResetState();
        }

        gameRestart.Invoke();           // HUD hides the popup, EnemyManager resets the rats
        scoreChange.Invoke(Score);
        StartTimer();
    }

    // Adds points to the score (e.g. a breakable object calls this when it breaks)
    public void AddScore(int amount)
    {
        if (!gameRunning) return;

        Score += amount;
        scoreChange.Invoke(Score);
    }

    // Called when the slashing cat touches a rat: +score, then the cat and the rats are reset
    public void PreyCaught()
    {
        if (!gameRunning || resettingAfterCatch) return;

        Score += scorePerCatch;
        scoreChange.Invoke(Score);
        Debug.Log("Prey caught! Score: " + Score);

        // Reset after the current physics callback has finished
        resettingAfterCatch = true;
        StartCoroutine(ResetAfterCatch());
    }

    private IEnumerator ResetAfterCatch()
    {
        yield return null;
        ResetPlayer();
        roundReset.Invoke();
        resettingAfterCatch = false;
    }

    private void ResetPlayer()
    {
        if (playerBody == null) return;

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

    // ---------------------------------------------------------------- timer

    private void StartTimer()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }
        TimeLeft = startTimeSeconds;
        gameRunning = true;
        timerRoutine = StartCoroutine(TimerTick());
    }

    private IEnumerator TimerTick()
    {
        while (gameRunning)
        {
            timeChange.Invoke(TimeLeft);
            if (TimeLeft <= 0)
            {
                TimeUp();
                yield break;
            }
            yield return new WaitForSeconds(1f);
            TimeLeft--;
        }
    }

    private void TimeUp()
    {
        gameRunning = false;
        Time.timeScale = 0.0f;
        gameOver.Invoke();
    }
}

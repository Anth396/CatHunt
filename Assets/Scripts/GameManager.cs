using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent game state (score, countdown, high score) that lives from the main menu onwards.
/// It announces changes through UnityEvents. Each level has a LevelContext that registers that
/// scene's player with it; UI lives in HUDManager, enemy resets in EnemyManager.
/// </summary>
public class GameManager : Singleton<GameManager>
{
    private const string HighScoreKey = "HighScore";

    [Header("Events")]
    public UnityEvent gameStart;            // the countdown has begun
    public UnityEvent gameRestart;          // full restart (score, timer, everything)
    public UnityEvent<int> scoreChange;     // new score
    public UnityEvent gameOver;             // time ran out
    public UnityEvent<int> timeChange;      // seconds left, once per second
    public UnityEvent roundReset;           // after a catch: player and rats go back to start

    [Header("Game Settings")]
    public int startTimeSeconds = 120;      // countdown start (2 minutes)
    public int scorePerCatch = 100;

    [Header("Data")]
    public GameConstants gameConstants;     // optional: overrides the two settings above
    public IntVariable gameScore;           // the score lives in this asset

    public int Score => gameScore.Value;
    public int TimeLeft { get; private set; }
    public bool IsRunning => gameRunning;

    // Saved between runs of the game, readable without a GameManager (e.g. before one exists)
    public static int SavedHighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    // Registered by the current level's LevelContext
    private Rigidbody2D playerBody;
    private string nextSceneName;

    private int levelStartScore = 0;   // score when the current level began
    private bool gameRunning = false;
    private bool resettingAfterCatch = false;
    private Coroutine timerRoutine;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;   // duplicate, being destroyed

        if (gameConstants != null)
        {
            startTimeSeconds = gameConstants.startTimeSeconds;
            scorePerCatch = gameConstants.scorePerCatch;
        }
        if (gameScore == null)
        {
            Debug.LogWarning("GameManager: no GameScore asset assigned, using a temporary one.", this);
            gameScore = ScriptableObject.CreateInstance<IntVariable>();
        }
    }

    void Start()
    {
        Application.targetFrameRate = 30;
    }

    // ---------------------------------------------------------------- flow

    // Main menu Start button: a brand new game
    public void StartNewGame()
    {
        gameScore.SetValue(0);
    }

    // Called by each level's LevelContext once the scene is loaded
    public void BeginLevel(Rigidbody2D body, string nextScene)
    {
        StopAllCoroutines();
        timerRoutine = null;
        gameRunning = false;
        resettingAfterCatch = false;
        Time.timeScale = 1.0f;

        playerBody = body;
        nextSceneName = nextScene;

        // Listen to the things that can happen in this scene's world
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
        StartCoroutine(StartLevelNextFrame());
    }

    private IEnumerator StartLevelNextFrame()
    {
        yield return null;
        // The score is kept from the previous level (StartNewGame resets it for a new game)
        levelStartScore = Score;    // what a restart of this level goes back to
        scoreChange.Invoke(Score);
        StartTimer();
        gameStart.Invoke();
    }

    // Time's up popup: keep the score and move on
    public void ContinueToNextLevel()
    {
        SaveHighScore();
        Time.timeScale = 1.0f;
        SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
    }

    // Leaving the level for the menu: stop the countdown so it does not tick in the background
    public void EndSession()
    {
        StopAllCoroutines();
        timerRoutine = null;
        gameRunning = false;
        resettingAfterCatch = false;
        Time.timeScale = 1.0f;
        SaveHighScore();
    }

    public void SaveHighScore()
    {
        if (Score > SavedHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, Score);
        }
        PlayerPrefs.Save();
    }

    // Hook this up to a UI Restart Button (through LevelContext.Restart)
    public void RestartButtonCallback(int input)
    {
        RestartGame();
    }

    public void RestartGame()
    {
        Debug.Log("Restart!");
        StopAllCoroutines();
        timerRoutine = null;
        resettingAfterCatch = false;
        Time.timeScale = 1.0f;

        gameScore.SetValue(levelStartScore);
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

        gameScore.ApplyChange(amount);
        scoreChange.Invoke(Score);
    }

    // Called when the slashing cat touches a rat: +score, then the cat and the rats are reset
    public void PreyCaught()
    {
        if (!gameRunning || resettingAfterCatch) return;

        gameScore.ApplyChange(scorePerCatch);
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
        SaveHighScore();
        gameOver.Invoke();
    }
}

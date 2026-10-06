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
    public GameObject popUpPanel;
    public TextMeshProUGUI finalScoreText;
    public GameObject enemies;
    public Transform playerTransform;
    public Rigidbody2D playerBody;

    [Header("Timer Settings")]
    [System.NonSerialized]
    public int timer = 0;
    private bool countTimerState = true;
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
        StartTimer();
    }

    public void StartTimer()
    {
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }
        timer = 0;
        countTimerState = true;
        timerRoutine = StartCoroutine(TimerTick());
    }

    private IEnumerator TimerTick()
    {
        while (countTimerState)
        {
            UpdateTimerText();
            yield return new WaitForSeconds(1f);
            timer++;
        }
    }

    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            int minutes = timer / 60;
            int seconds = timer % 60;
            timerText.text = string.Format("Timer - {0:00}:{1:00}", minutes, seconds);
        }
    }

    public void StopGameAndShowWin(int finalTime)
    {
        if (!countTimerState) return; // already won
        countTimerState = false;
        Time.timeScale = 0.0f;

        if (popUpPanel != null && finalScoreText != null)
        {
            int minutes = finalTime / 60;
            int seconds = finalTime % 60;
            string finalTimeStr = string.Format("{0:00}:{1:00}", minutes, seconds);

            finalScoreText.text = "Prey Caught!\nYour Time: " + finalTimeStr;
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

    private void ResetGame()
    {
        // 1. Reset the player (pose, velocity, dash / slash / hold state)
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

        // 2. Hide pop-up panel
        if (popUpPanel != null)
        {
            popUpPanel.SetActive(false);
        }

        // 3. Reset every rat (pose, velocity, AI state)
        foreach (EnemyMovement enemyMove in FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None))
        {
            enemyMove.ResetState();
        }

        // 4. Restore every broken object
        foreach (BreakableObject breakable in FindObjectsByType<BreakableObject>(FindObjectsSortMode.None))
        {
            breakable.ResetState();
        }

        // 5. Restart timer
        StartTimer();
    }
}
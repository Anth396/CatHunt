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
        // 1. Reset player position and velocity
        if (playerBody != null)
        {
            playerBody.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
            playerBody.linearVelocity = Vector2.zero;
        }

        // 2. Hide pop-up panel
        if (popUpPanel != null)
        {
            popUpPanel.SetActive(false);
        }

        // 3. Reset all enemies to start positions
        if (enemies != null)
        {
            foreach (Transform eachChild in enemies.transform)
            {
                EnemyMovement enemyMove = eachChild.GetComponent<EnemyMovement>();
                if (enemyMove != null)
                {
                    eachChild.transform.localPosition = enemyMove.startPosition;
                }
            }
        }

        // 4. Restart timer
        StartTimer();
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CatchThePrey : MonoBehaviour
{
    public Transform enemyLocation;
    public TextMeshProUGUI timerText;
    private bool onGroundState;

    [System.NonSerialized]
    public int timer = 0; // we don't want this to show up in the inspector

    private bool countTimerState = true;
    public Vector3 boxSize;
    public float maxDistance;
    public LayerMask layerMask;
    private Coroutine timerRoutine;

    [Header("Pop-Up UI Components")]
    public GameObject popUpPanel;             // Drag PopUpPanel here
    public TextMeshProUGUI finalScoreText; 

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(TimerTick());
    }
    public void RestartTimer()
    {
        // 1. If a timer loop is already running, safely stop it first
        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }

        // 2. Reset values to default configuration
        timer = 0;
        countTimerState = true;

        // 3. Start the timer routine freshly and save its reference
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

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object we touched is the enemy
        if (other.gameObject.CompareTag("Enemy") || other.transform == enemyLocation)
        {
            countTimerState = false; // This stops the timer loop
            Debug.Log("Enemy caught! Timer stopped at: " + timer);
            ShowPopUpMessage();
        }
    }
    private void ShowPopUpMessage()
    {
        if (popUpPanel != null && finalScoreText != null)
        {
            // 1. Calculate the final formatted time string
            int minutes = timer / 60;
            int seconds = timer % 60;
            string finalTimeStr = string.Format("{0:00}:{1:00}", minutes, seconds);

            // 2. Set the text message on your pop-up window
            finalScoreText.text = "Prey Caught!\nYour Time: " + finalTimeStr;

            // 3. Activate the panel gameobject so it pops up visually
            popUpPanel.SetActive(true);
        }
    }

}

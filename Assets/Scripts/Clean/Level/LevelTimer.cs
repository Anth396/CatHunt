using System.Collections;
using UnityEngine;

/// <summary>
/// The level countdown. StartTimer is the response of the GameStart and GameRestart listeners.
/// It raises TimeChanged every second and GameOver when time runs out.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    public GameConstants constants;
    public IntGameEvent timeChanged;
    public SimpleGameEvent gameOver;
    public BoolVariable gameRunning;

    private Coroutine routine;

    public void StartTimer()
    {
        StopTimer();
        routine = StartCoroutine(Tick());
    }

    public void StopTimer()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator Tick()
    {
        int timeLeft = constants != null ? constants.startTimeSeconds : 120;
        if (gameRunning != null) gameRunning.SetValue(true);

        while (true)
        {
            if (timeChanged != null) timeChanged.Raise(timeLeft);
            if (timeLeft <= 0) break;
            yield return new WaitForSeconds(1f);     // scaled time, so pausing stops the clock
            timeLeft--;
        }

        if (gameRunning != null) gameRunning.SetValue(false);
        Time.timeScale = 0f;
        routine = null;
        if (gameOver != null) gameOver.Raise();
    }
}

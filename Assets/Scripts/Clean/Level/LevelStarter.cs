using System.Collections;
using UnityEngine;

/// <summary>
/// Announces that the level has begun. It waits one frame so every listener in the scene is registered,
/// then raises GameStart. It knows nothing about who reacts.
/// </summary>
public class LevelStarter : MonoBehaviour
{
    public SimpleGameEvent gameStart;
    [Tooltip("Shared flag: is a round in progress? Set to false here, the timer turns it on.")]
    public BoolVariable gameRunning;

    IEnumerator Start()
    {
        // The previous level may have left time frozen or the audio paused
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (gameRunning != null) gameRunning.SetValue(false);

        yield return null;
        if (gameStart != null) gameStart.Raise();
    }
}

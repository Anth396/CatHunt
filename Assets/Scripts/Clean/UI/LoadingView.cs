using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loading scene: shows the name of the scene to load (read from the LoadingTarget asset), waits a moment
/// so it can be read, then loads it. The Main Menu button can still cancel during the wait.
/// </summary>
public class LoadingView : MonoBehaviour
{
    public StringVariable loadingTarget;
    public TextMeshProUGUI levelText;
    public float minimumSeconds = 1.5f;
    [Tooltip("Used when the loading scene is opened directly (e.g. pressing Play in the editor).")]
    public string fallbackScene = "city";
    [Tooltip("Raised when the player cancels; SceneChanger then loads the main menu.")]
    public SimpleGameEvent mainMenuRequested;

    private bool loadStarted;

    IEnumerator Start()
    {
        string target = loadingTarget != null && !string.IsNullOrEmpty(loadingTarget.Value)
            ? loadingTarget.Value
            : fallbackScene;

        if (levelText != null) levelText.text = char.ToUpper(target[0]) + target.Substring(1);

        yield return new WaitForSecondsRealtime(minimumSeconds);

        loadStarted = true;     // a started load cannot be cancelled safely
        SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
    }

    // Main Menu button
    public void CancelToMainMenu()
    {
        if (loadStarted) return;
        StopAllCoroutines();
        if (mainMenuRequested != null) mainMenuRequested.Raise();
    }
}

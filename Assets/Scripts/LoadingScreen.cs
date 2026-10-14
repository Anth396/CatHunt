using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loading scene: shows the name of the scene being loaded, waits a moment so the text can be
/// read, then loads it. Put it in the loading scene and assign the text.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public TextMeshProUGUI levelText;
    [Tooltip("How long the loading screen stays up before the scene starts loading.")]
    public float minimumSeconds = 1.5f;
    [Tooltip("Used when the loading scene is opened directly (e.g. pressing Play in the editor).")]
    public string fallbackScene = "city";

    private bool loadStarted = false;

    void Start()
    {
        string target = string.IsNullOrEmpty(SceneLoader.TargetScene) ? fallbackScene : SceneLoader.TargetScene;
        if (levelText != null) levelText.text = DisplayName(target);
        StartCoroutine(LoadAfterDelay(target));
    }

    private IEnumerator LoadAfterDelay(string target)
    {
        yield return new WaitForSecondsRealtime(minimumSeconds);

        loadStarted = true;     // from here the load cannot be cancelled
        SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
    }

    // Hook this up to the loading scene's Main Menu button
    public void ReturnToMainMenu()
    {
        if (loadStarted) return;
        StopAllCoroutines();
        SceneLoader.LoadMainMenu();
    }

    // "city" -> "City"
    private static string DisplayName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return "";
        return char.ToUpper(sceneName[0]) + sceneName.Substring(1);
    }
}

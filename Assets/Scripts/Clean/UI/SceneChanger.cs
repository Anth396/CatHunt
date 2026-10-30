using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Changes scenes in response to events. SceneRequested goes through the loading scene (the target
/// name is kept in the LoadingTarget asset, which survives the scene change); MainMenuRequested loads the
/// main menu directly. One per scene, nothing is kept alive between scenes.
/// </summary>
public class SceneChanger : MonoBehaviour
{
    public StringVariable loadingTarget;
    public string loadingScene = "loading";
    public string mainMenuScene = "mainmenu";

    // SceneRequested response (string payload = scene to load)
    public void OnSceneRequested(string targetScene)
    {
        if (loadingTarget != null) loadingTarget.SetValue(targetScene);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(loadingScene);
    }

    // MainMenuRequested response
    public void OnMainMenuRequested()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(mainMenuScene);
    }
}

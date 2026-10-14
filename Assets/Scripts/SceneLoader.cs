using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene changes. Going into a level or between levels goes through the loading scene; going back
/// to the main menu loads it directly.
/// </summary>
public static class SceneLoader
{
    public const string LoadingScene = "loading";
    public const string MainMenuScene = "mainmenu";

    // The scene the loading scene should load (read by LoadingScreen)
    public static string TargetScene { get; private set; }

    // Opens the loading scene, which then loads the target
    public static void LoadThroughLoadingScreen(string targetScene)
    {
        TargetScene = targetScene;
        ResetRuntimeState();
        SceneManager.LoadScene(LoadingScene);
    }

    // No loading scene for the main menu
    public static void LoadMainMenu()
    {
        ResetRuntimeState();
        SceneManager.LoadScene(MainMenuScene);
    }

    // Time and audio pause are global, so a scene change must never carry them over
    private static void ResetRuntimeState()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}

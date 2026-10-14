using UnityEngine;

/// <summary>
/// One per level scene. Registers this scene's player with the persistent GameManager and
/// forwards the level's buttons to it (a button in a scene cannot reference an object in another scene).
/// </summary>
public class LevelContext : MonoBehaviour
{
    [Tooltip("The cat's Rigidbody2D in this scene.")]
    public Rigidbody2D playerBody;
    [Tooltip("Scene loaded by the Continue button after time runs out.")]
    public string nextSceneName = "sewer";

    void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("LevelContext: no GameManager. Start the game from the mainmenu scene.", this);
            return;
        }
        GameManager.Instance.BeginLevel(playerBody, nextSceneName);
    }

    // Hook this up to the Continue button on the time's up popup
    public void Continue()
    {
        if (GameManager.Instance != null) GameManager.Instance.ContinueToNextLevel();
    }

    // Hook this up to the Restart button on the time's up popup
    public void Restart()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
    }
}

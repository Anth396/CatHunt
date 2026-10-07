using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resets the rats when the GameManager announces a restart or a new round (after a catch).
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Tooltip("Leave empty to manage every EnemyMovement in the scene.")]
    public List<EnemyMovement> enemies = new List<EnemyMovement>();

    private GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("EnemyManager: no GameManager in the scene.", this);
            return;
        }

        gm.gameRestart.AddListener(ResetEnemies);
        gm.roundReset.AddListener(ResetEnemies);
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.gameRestart.RemoveListener(ResetEnemies);
        gm.roundReset.RemoveListener(ResetEnemies);
    }

    public void ResetEnemies()
    {
        IEnumerable<EnemyMovement> targets = enemies.Count > 0
            ? enemies
            : (IEnumerable<EnemyMovement>)FindObjectsByType<EnemyMovement>(FindObjectsSortMode.None);

        foreach (EnemyMovement enemy in targets)
        {
            if (enemy != null) enemy.ResetState();
        }
    }
}

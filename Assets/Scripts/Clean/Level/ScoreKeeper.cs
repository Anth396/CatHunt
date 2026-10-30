using UnityEngine;

/// <summary>
/// Owns score changes. The score itself lives in the GameScore asset, so it survives scene loads.
/// A restart returns to the score the level started with, not to zero.
/// </summary>
public class ScoreKeeper : MonoBehaviour
{
    public IntVariable gameScore;
    [Tooltip("The score when the current level began (a restart goes back to it).")]
    public IntVariable levelStartScore;
    public GameConstants constants;
    public BoolVariable gameRunning;
    public IntGameEvent scoreChanged;

    // GameStart response
    public void OnGameStart()
    {
        levelStartScore.SetValue(gameScore.Value);
        Publish();
    }

    // GameRestart response
    public void OnGameRestart()
    {
        gameScore.SetValue(levelStartScore.Value);
        Publish();
    }

    // PreyCaught response
    public void OnPreyCaught()
    {
        AddScore(constants != null ? constants.scorePerCatch : 100);
    }

    // ObjectBroken response (int payload = points)
    public void AddScore(int amount)
    {
        if (gameRunning != null && !gameRunning.Value) return;

        gameScore.ApplyChange(amount);
        Publish();
    }

    private void Publish()
    {
        if (scoreChanged != null) scoreChanged.Raise(gameScore.Value);
    }
}

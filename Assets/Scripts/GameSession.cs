using UnityEngine;

/// <summary>
/// Data that has to outlive a scene load: the score carried into the next level and the saved high score.
/// </summary>
public static class GameSession
{
    private const string HighScoreKey = "HighScore";

    // Score the next level starts with (0 for a new game, the final score when continuing)
    public static int StartScore = 0;

    public static int HighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    // Stores the score if it beats the saved one
    public static void SubmitScore(int score)
    {
        if (score > HighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
        }
    }
}

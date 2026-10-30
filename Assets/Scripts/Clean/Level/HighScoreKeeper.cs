using UnityEngine;

/// <summary>Keeps the best score between runs of the game (PlayerPrefs), so the main menu can show it.</summary>
public class HighScoreKeeper : MonoBehaviour
{
    public const string Key = "HighScore";

    public IntVariable gameScore;

    public static int SavedHighScore => PlayerPrefs.GetInt(Key, 0);

    // GameOver response; also called when leaving a level for the main menu
    public void Save()
    {
        if (gameScore != null && gameScore.Value > SavedHighScore)
        {
            PlayerPrefs.SetInt(Key, gameScore.Value);
        }
        PlayerPrefs.Save();
    }
}

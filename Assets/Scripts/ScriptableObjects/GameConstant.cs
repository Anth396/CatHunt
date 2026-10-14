using UnityEngine;

/// <summary>
/// Tunable numbers for the whole game in one asset. The default values below match what the
/// scripts used before, so a freshly created asset changes nothing until you edit it.
/// </summary>
[CreateAssetMenu(fileName = "GameConstants", menuName = "ScriptableObjects/GameConstants", order = 1)]
public class GameConstants : ScriptableObject
{
    [Header("Game")]
    public int startTimeSeconds = 120;      // countdown start (2 minutes)
    public int scorePerCatch = 100;

    [Header("Cat - Movement")]
    public float catMoveSpeed = 10f;
    public float catMaxSpeed = 15f;
    public float catJumpForce = 12f;

    [Header("Cat - Dash")]
    public float catDashSpeed = 25f;
    public float catDashDuration = 0.5f;
    public float catDashCooldown = 1.5f;
    public float catSuperDashSpeed = 40f;
    public float catSuperDashDuration = 0.7f;

    [Header("Rat - Movement")]
    public float ratMaxSpeed = 15f;
    public float ratWalkSpeed = 5f;
    public float ratJumpForce = 15f;
}

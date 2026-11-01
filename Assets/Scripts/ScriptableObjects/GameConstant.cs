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

    [Header("Cat - Physics")]
    public float catGravity = 30f;
    public float catFallMultiplier = 1.5f;
    public float catGroundFriction = 15f;
    public float catBounceMinSpeed = 5f;

    [Header("Cat - Slash / Stun")]
    public float catSlashDuration = 0.35f;
    public float catSlashCooldown = 0.3f;
    public float catStunDuration = 0.3f;

    [Header("Cat - Wall Cling")]
    public float catWallJumpSpeedX = 10f;       // horizontal speed away from the wall
    public float catWallJumpSpeedY = 15f;       // upward speed
    public float catWallJumpTime = 0.25f;       // input is locked this long so the cat is not pulled back on
    public float catWallSlideDelay = 0.3f;      // the cat holds still this long, then starts to slide down
    public float catWallSlideSpeed = 2f;        // downward speed while sliding

    [Header("Cat - Buffs")]
    public float hasteSpeedMultiplier = 1.5f;
    public float invisibleAlpha = 0.35f;

    [Header("Rat - Movement")]
    public float ratMaxSpeed = 15f;
    public float ratWalkSpeed = 5f;
    public float ratJumpForce = 15f;
}

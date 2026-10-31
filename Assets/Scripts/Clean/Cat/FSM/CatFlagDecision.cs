using UnityEngine;

public enum CatFlag
{
    DashPressed,
    DashReleased,           // the dash key is no longer held
    DashReleasedAfterSuper, // released after being held long enough for a super dash
    SlashPressed,
    JumpPressed,
    ObstacleHit,
    NotGrounded,
    Grounded,
    ClingToWall,            // in the air, touching a wall, and the cling (shift) key is held
    ClingReleased,          // the cling (shift) key is no longer held
    LostWall                // no wall under the cat's hands any more
}

/// <summary>True when the chosen input / contact flag of the cat is set.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Decisions/CatFlag")]
public class CatFlagDecision : Decision
{
    public CatFlag flag = CatFlag.DashPressed;

    public override bool Decide(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        switch (flag)
        {
            case CatFlag.DashPressed: return cat.dashPressed;
            case CatFlag.DashReleased: return !cat.dashHeld;
            case CatFlag.DashReleasedAfterSuper: return !cat.dashHeld && cat.superReady;
            case CatFlag.SlashPressed: return cat.slashPressed;
            case CatFlag.JumpPressed: return cat.jumpPressed;
            case CatFlag.ObstacleHit: return cat.obstacleHit;
            case CatFlag.NotGrounded: return !cat.motor.IsGrounded;
            case CatFlag.Grounded: return cat.motor.IsGrounded;
            case CatFlag.ClingToWall: return cat.dashHeld && !cat.motor.IsGrounded && cat.motor.WallSide != 0;
            case CatFlag.ClingReleased: return !cat.dashHeld;
            case CatFlag.LostWall: return cat.motor.WallSide == 0;
            default: return false;
        }
    }
}

using UnityEngine;

public enum CatFlag
{
    DashPressed,
    DashReleased,           // the dash key is no longer held
    DashReleasedAfterSuper, // released after being held long enough for a super dash
    SlashPressed,
    JumpPressed,
    ObstacleHit,
    NotGrounded
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
            default: return false;
        }
    }
}

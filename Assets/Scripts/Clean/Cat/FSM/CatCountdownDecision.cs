using UnityEngine;

public enum CatDuration
{
    Dash,
    SuperDash,
    Slash,
    Stun,
    WallJump
}

/// <summary>True once the cat has stayed in the state for its duration, which comes from GameConstants.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Decisions/CatCountdown")]
public class CatCountdownDecision : Decision
{
    public CatDuration duration = CatDuration.Dash;

    public override bool Decide(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        GameConstants c = cat.Constants;
        float seconds;
        switch (duration)
        {
            case CatDuration.Dash: seconds = c.catDashDuration; break;
            case CatDuration.SuperDash: seconds = c.catSuperDashDuration; break;
            case CatDuration.Slash: seconds = c.catSlashDuration; break;
            case CatDuration.WallJump: seconds = c.catWallJumpTime; break;
            default: seconds = c.catStunDuration; break;
        }
        return cat.CheckIfCountDownElapsed(seconds);
    }
}

using UnityEngine;

/// <summary>True once the current buff has lasted as long as the collected powerup says.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Decisions/BuffExpired")]
public class BuffExpiredDecision : Decision
{
    public override bool Decide(StateController controller)
    {
        CatBuffController buff = (CatBuffController)controller;
        return buff.CheckIfCountDownElapsed(buff.activeDuration);
    }
}

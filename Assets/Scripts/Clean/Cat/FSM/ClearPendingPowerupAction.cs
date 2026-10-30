using UnityEngine;

/// <summary>Marks the collected powerup as handled once a buff state has been entered.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/ClearPendingPowerup")]
public class ClearPendingPowerupAction : Action
{
    public override void Act(StateController controller)
    {
        ((CatBuffController)controller).pendingPowerup = PowerupType.None;
    }
}

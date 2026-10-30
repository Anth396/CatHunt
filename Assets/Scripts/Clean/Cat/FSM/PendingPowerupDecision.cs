using UnityEngine;

/// <summary>True when the powerup just collected is of the given type.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Decisions/PendingPowerup")]
public class PendingPowerupDecision : Decision
{
    public PowerupType powerupType = PowerupType.Invisibility;

    public override bool Decide(StateController controller)
    {
        return ((CatBuffController)controller).pendingPowerup == powerupType;
    }
}

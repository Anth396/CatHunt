using UnityEngine;

/// <summary>Starts a dash (or super dash) and the dash cooldown.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/StartDash")]
public class StartDashAction : Action
{
    public bool super = false;

    public override void Act(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        cat.motor.StartDash(super);
        cat.dashCooldownTimer = cat.Constants.catDashCooldown;
        cat.superReady = false;
    }
}

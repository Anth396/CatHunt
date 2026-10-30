using UnityEngine;

/// <summary>Starts the wait before the cat can slash again (used when a slash ends).</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/StartSlashCooldown")]
public class StartSlashCooldownAction : Action
{
    public override void Act(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        cat.slashCooldownTimer = cat.Constants.catSlashCooldown;
    }
}

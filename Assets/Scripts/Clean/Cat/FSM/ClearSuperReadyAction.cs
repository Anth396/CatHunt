using UnityEngine;

/// <summary>Forgets a previous "dash held long enough" flag, so each charge starts fresh.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/ClearSuperReady")]
public class ClearSuperReadyAction : Action
{
    public override void Act(StateController controller)
    {
        ((CatStateController)controller).superReady = false;
    }
}

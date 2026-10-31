using UnityEngine;

/// <summary>Grabs the wall the cat is touching and holds it there.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/StartCling")]
public class StartClingAction : Action
{
    public override void Act(StateController controller)
    {
        ((CatStateController)controller).motor.StartCling();
    }
}

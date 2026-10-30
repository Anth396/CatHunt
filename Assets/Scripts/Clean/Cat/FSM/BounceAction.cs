using UnityEngine;

/// <summary>Knocks the cat back (obstacle hit) and locks its input.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/Bounce")]
public class BounceAction : Action
{
    public override void Act(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        cat.motor.Bounce();
    }
}

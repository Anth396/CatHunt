using UnityEngine;

/// <summary>Ends the dash and hands the motor back to normal running.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/EndDash")]
public class EndDashAction : Action
{
    public override void Act(StateController controller)
    {
        CatStateController cat = (CatStateController)controller;
        cat.motor.EndDash();
    }
}

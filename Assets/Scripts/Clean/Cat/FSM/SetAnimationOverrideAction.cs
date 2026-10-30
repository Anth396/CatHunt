using UnityEngine;

/// <summary>Forces an animation state on the cat. Leave the name empty to hand control back to the motor.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/SetAnimationOverride")]
public class SetAnimationOverrideAction : Action
{
    public string animationState = "";

    public override void Act(StateController controller)
    {
        CatMotor motor = controller.GetComponent<CatMotor>();
        if (motor != null) motor.AnimationOverride = animationState;
    }
}

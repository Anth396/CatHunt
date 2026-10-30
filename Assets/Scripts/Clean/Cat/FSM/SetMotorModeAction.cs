using UnityEngine;

/// <summary>Switches the cat's motor to the given mode (Free, Brake, Locked).</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/SetMotorMode")]
public class SetMotorModeAction : Action
{
    public CatMotorMode mode = CatMotorMode.Free;

    public override void Act(StateController controller)
    {
        CatMotor motor = controller.GetComponent<CatMotor>();
        if (motor != null) motor.SetMode(mode);
    }
}

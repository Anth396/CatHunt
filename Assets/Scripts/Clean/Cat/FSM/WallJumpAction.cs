using UnityEngine;

/// <summary>Kicks the cat off the wall it is holding: away from the wall and upwards.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/WallJump")]
public class WallJumpAction : Action
{
    public override void Act(StateController controller)
    {
        ((CatStateController)controller).motor.WallJump();
    }
}

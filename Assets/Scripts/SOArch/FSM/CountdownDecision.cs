using UnityEngine;

/// <summary>True once the controller has stayed in the current state for 'duration' seconds.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Decisions/Countdown")]
public class CountdownDecision : Decision
{
    public float duration = 1f;

    public override bool Decide(StateController controller)
    {
        return controller.CheckIfCountDownElapsed(duration);
    }
}

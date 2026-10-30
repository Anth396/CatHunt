using UnityEngine;

/// <summary>A yes/no question a transition asks about the controller.</summary>
public abstract class Decision : ScriptableObject
{
    public abstract bool Decide(StateController controller);
}

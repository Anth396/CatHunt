using UnityEngine;

/// <summary>Something a state does. Each concrete action is its own asset.</summary>
public abstract class Action : ScriptableObject
{
    public abstract void Act(StateController controller);
}

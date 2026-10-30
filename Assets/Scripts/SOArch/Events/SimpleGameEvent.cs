using UnityEngine;

/// <summary>An event with no payload (uses the dummy Void type).</summary>
[CreateAssetMenu(fileName = "SimpleGameEvent", menuName = "ScriptableObjects/Events/SimpleGameEvent", order = 3)]
public class SimpleGameEvent : GameEvent<Void>
{
    // Parameterless overload so it can be called from code and wired to a UnityEvent in the Inspector
    public void Raise()
    {
        Raise(new Void());
    }
}

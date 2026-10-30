using UnityEngine;

/// <summary>An event carrying one int (score, seconds left, direction...).</summary>
[CreateAssetMenu(fileName = "IntGameEvent", menuName = "ScriptableObjects/Events/IntGameEvent", order = 4)]
public class IntGameEvent : GameEvent<int>
{
}

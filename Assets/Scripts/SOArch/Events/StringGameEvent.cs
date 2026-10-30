using UnityEngine;

/// <summary>An event carrying one string (e.g. the name of the scene to load).</summary>
[CreateAssetMenu(fileName = "StringGameEvent", menuName = "ScriptableObjects/Events/StringGameEvent", order = 5)]
public class StringGameEvent : GameEvent<string>
{
}

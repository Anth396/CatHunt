using UnityEngine;

/// <summary>An event carrying the powerup that was just collected.</summary>
[CreateAssetMenu(fileName = "PowerupGameEvent", menuName = "ScriptableObjects/Events/PowerupGameEvent", order = 6)]
public class PowerupGameEvent : GameEvent<IPowerup>
{
}

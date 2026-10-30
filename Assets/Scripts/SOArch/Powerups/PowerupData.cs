using UnityEngine;

/// <summary>A powerup defined as an asset: its type and how long the buff lasts.</summary>
[CreateAssetMenu(fileName = "PowerupData", menuName = "ScriptableObjects/PowerupData", order = 6)]
public class PowerupData : ScriptableObject, IPowerup
{
    public PowerupType type = PowerupType.Invisibility;
    public float duration = 8f;         // seconds
    public string displayName = "Powerup";

    public PowerupType Type => type;
    public float Duration => duration;
    public string DisplayName => displayName;
}

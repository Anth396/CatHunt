/// <summary>Kinds of powerup, used by the buff state machine's decisions.</summary>
public enum PowerupType
{
    None = -1,
    Invisibility = 0,
    Haste = 1
}

/// <summary>Anything the cat can pick up that grants a timed buff.</summary>
public interface IPowerup
{
    PowerupType Type { get; }
    float Duration { get; }
    string DisplayName { get; }
}

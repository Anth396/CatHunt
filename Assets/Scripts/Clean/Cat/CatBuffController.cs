using UnityEngine;

/// <summary>Names of the cat's buff states. The state assets are named exactly like these.</summary>
public enum CatBuffState
{
    NoBuff,
    Invisible,
    Haste
}

/// <summary>
/// Second state machine: which timed buff the cat has (none, invisible, haste). A collected powerup
/// arrives through PowerupCollected; the states' decisions pick the transition and the countdown.
/// </summary>
public class CatBuffController : StateController
{
    [Header("Cat")]
    public CatMotor motor;
    [Tooltip("Rats read this to know whether they can see the cat.")]
    public BoolVariable catInvisible;

    // Set by OnPowerupCollected, cleared when a state has reacted to it
    [HideInInspector] public PowerupType pendingPowerup = PowerupType.None;
    [HideInInspector] public float activeDuration;

    private SpriteRenderer sprite;

    public GameConstants Constants => motor != null ? motor.constants : null;

    public CatBuffState BuffState =>
        currentState == null ? CatBuffState.NoBuff : EnumExtension.ParseEnum<CatBuffState>(currentState.name);

    public override void Start()
    {
        if (motor == null) motor = GetComponent<CatMotor>();
        sprite = GetComponent<SpriteRenderer>();
        base.Start();
        ResetState();
    }

    // PowerupCollected listener response
    public void OnPowerupCollected(IPowerup powerup)
    {
        activeDuration = powerup.Duration;

        // Picking up the buff we already have just restarts its countdown
        if (BuffOfCurrentState() == powerup.Type)
        {
            stateTimeElapsed = 0f;
            pendingPowerup = PowerupType.None;
            return;
        }
        pendingPowerup = powerup.Type;
    }

    private PowerupType BuffOfCurrentState()
    {
        switch (BuffState)
        {
            case CatBuffState.Invisible: return PowerupType.Invisibility;
            case CatBuffState.Haste: return PowerupType.Haste;
            default: return PowerupType.None;
        }
    }

    public void SetSpriteColor(Color color)
    {
        if (sprite != null) sprite.color = color;
    }

    // GameRestart / RoundReset: lose any buff
    public void ResetState()
    {
        pendingPowerup = PowerupType.None;
        stateTimeElapsed = 0f;
        TransitionToState(startState);
    }
}

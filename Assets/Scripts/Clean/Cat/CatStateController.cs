using UnityEngine;

/// <summary>Names of the cat's movement states. The state assets are named exactly like these.</summary>
public enum CatMoveState
{
    Normal,
    Charging,
    Dashing,
    SuperDashing,
    Slashing,
    Stunned,
    WallClinging,
    WallJumping
}

/// <summary>
/// State machine for what the cat is doing (running, charging a dash, dashing, slashing, stunned).
/// Input and hits arrive as public callbacks, wired by GameEventListener components; the states'
/// decisions read the flags set here.
/// </summary>
public class CatStateController : StateController
{
    [Header("Cat")]
    public CatMotor motor;
    public AudioClip superReadyClip;

    // Held / persistent input
    [HideInInspector] public bool dashHeld;
    [HideInInspector] public bool superReady;

    // One-frame input: valid for the next state update, then cleared
    [HideInInspector] public bool dashPressed;
    [HideInInspector] public bool slashPressed;
    [HideInInspector] public bool jumpPressed;
    [HideInInspector] public bool obstacleHit;

    [HideInInspector] public float dashCooldownTimer;
    [HideInInspector] public float slashCooldownTimer;

    public GameConstants Constants => motor != null ? motor.constants : null;

    public CatMoveState MoveState =>
        currentState == null ? CatMoveState.Normal : EnumExtension.ParseEnum<CatMoveState>(currentState.name);

    public bool IsDashing => MoveState == CatMoveState.Dashing || MoveState == CatMoveState.SuperDashing;
    public bool IsSlashing => MoveState == CatMoveState.Slashing;

    public override void Start()
    {
        if (motor == null) motor = GetComponent<CatMotor>();
        base.Start();
        ResetState();
    }

    public override void Update()
    {
        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        if (slashCooldownTimer > 0f) slashCooldownTimer -= Time.deltaTime;
        base.Update();
    }

    void LateUpdate()
    {
        dashPressed = false;
        slashPressed = false;
        jumpPressed = false;
        obstacleHit = false;
    }

    // ------------------------------------------------ callbacks for GameEventListener responses

    public void OnDashPressed()
    {
        dashHeld = true;
        if (motor != null) motor.ClingKeyHeld = true;
        if (dashCooldownTimer <= 0f && motor != null && motor.IsGrounded) dashPressed = true;
    }

    public void OnDashReleased()
    {
        // superReady stays set: the Charging state reads it when it sees the release, and it is
        // cleared when the next charge starts
        dashHeld = false;
        if (motor != null) motor.ClingKeyHeld = false;
    }

    // The Hold interaction fired: releasing now performs a super dash
    public void OnSuperDashReady()
    {
        if (MoveState != CatMoveState.Charging || superReady) return;
        superReady = true;
        PlayClip(superReadyClip);
    }

    public void OnSlashPressed()
    {
        if (slashCooldownTimer <= 0f) slashPressed = true;
    }

    public void OnJumpPressed()
    {
        jumpPressed = true;
    }

    public void OnObstacleHit()
    {
        obstacleHit = true;
    }

    // GameRestart / RoundReset
    public void ResetState()
    {
        dashHeld = false;
        if (motor != null) motor.ClingKeyHeld = false;
        superReady = false;
        dashPressed = false;
        slashPressed = false;
        jumpPressed = false;
        obstacleHit = false;
        dashCooldownTimer = 0f;
        slashCooldownTimer = 0f;
        stateTimeElapsed = 0f;
        TransitionToState(startState);
    }
}

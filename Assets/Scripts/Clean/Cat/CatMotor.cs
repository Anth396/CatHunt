using UnityEngine;

public enum CatMotorMode
{
    Free,       // normal running and jumping
    Brake,      // standing still while charging a dash (facing can still change)
    Dash,       // fixed-speed dash
    Locked,     // ignores input (stunned)
    Cling       // holding on to a wall: no gravity, no movement
}

/// <summary>
/// The cat's body: custom physics (gravity, running, jumping, dashing) and animation.
/// It decides nothing about game rules. The state machines (CatStateController) set its mode, and
/// input arrives through GameEventListener components calling SetMoveInput / Jump.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CatMotor : MonoBehaviour
{
    [Header("Data")]
    public GameConstants constants;
    public string groundTag = "Ground";
    public string wallTag = "Wall";

    [Header("Sound")]
    public AudioSource jumpAudio;

    // Set by the state machines
    public CatMotorMode Mode { get; private set; } = CatMotorMode.Free;
    public string AnimationOverride { get; set; }       // null = pick the animation from movement
    public float SpeedMultiplier { get; set; } = 1f;    // haste buff

    public bool IsGrounded => onGround;

    // -1 = a wall on the left, 1 = on the right, 0 = not touching one (tag "Wall")
    public int WallSide { get; private set; }

    // The cling key (shift) is held; set by the state machine. Building walls are only solid to a cat
    // that is in the air with this key held.
    public bool ClingKeyHeld { get; set; }
    public bool FacingRight => sprite == null || !sprite.flipX;
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
    public Bounds Bounds => cachedCollider != null ? cachedCollider.bounds : new Bounds(transform.position, Vector3.one);

    // Values copied from the constants asset
    private float moveSpeed, maxSpeed, jumpForce;
    private float gravity, fallMultiplier, groundFriction, bounceMinSpeed;

    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Animator animator;
    private Collider2D cachedCollider;

    private bool onGround = false;
    private bool jumpRequest = false;

    // Walls that do not stop the cat on the ground (PassableWhenGrounded markers)
    private readonly System.Collections.Generic.List<Collider2D> passableWalls = new System.Collections.Generic.List<Collider2D>();
    private bool ignoringWalls = false;
    private int moveInput = 0;
    private float horizontalInput = 0f;
    private float airMomentumX = 0f;
    private float dashDirection = 1f;
    private float currentDashSpeed = 0f;
    private string currentAnimation;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private bool initialFlipX;

    const string ANIM_IDLE = "cat-idle";
    const string ANIM_RUN = "cat-run";
    const string ANIM_JUMP = "cat-jump";
    const string ANIM_MID_JUMP = "cat-mid-jump";
    const string ANIM_MID_FALL = "cat-mid-fall";
    const string ANIM_FALL = "cat-fall";
    const string ANIM_SKID = "cat-skid";

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        cachedCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        if (constants == null)
        {
            Debug.LogWarning("CatMotor: no GameConstants assigned, using the defaults.", this);
            constants = ScriptableObject.CreateInstance<GameConstants>();
        }
        moveSpeed = constants.catMoveSpeed;
        maxSpeed = constants.catMaxSpeed;
        jumpForce = constants.catJumpForce;
        gravity = constants.catGravity;
        fallMultiplier = constants.catFallMultiplier;
        groundFriction = constants.catGroundFriction;
        bounceMinSpeed = constants.catBounceMinSpeed;

        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 0f;     // gravity is applied here
        // Edge colliders have no thickness; a fast fall or dash can cross one within a single step
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        initialFlipX = sprite != null && sprite.flipX;

        foreach (PassableWhenGrounded wall in FindObjectsByType<PassableWhenGrounded>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            passableWalls.AddRange(wall.GetComponents<Collider2D>());
        }
    }

    // Those walls are solid only for a cat in the air that holds the cling key. On the ground, or in
    // the air without the key, the cat passes straight through them.
    private void UpdateWallCollision()
    {
        bool ignore = onGround || !ClingKeyHeld;
        if (ignore == ignoringWalls) return;

        ignoringWalls = ignore;
        foreach (Collider2D wall in passableWalls)
        {
            if (wall != null) Physics2D.IgnoreCollision(cachedCollider, wall, ignoringWalls);
        }
        if (ignoringWalls) WallSide = 0;    // the contact is gone, so forget the wall
    }

    // ------------------------------------------------ called by listeners / state machine actions

    public void SetMoveInput(int direction)
    {
        moveInput = direction;
    }

    public void Jump()
    {
        if (Time.timeScale == 0f) return;
        if (!onGround || Mode == CatMotorMode.Locked) return;

        jumpRequest = true;
        if (jumpAudio != null && jumpAudio.clip != null) jumpAudio.PlayOneShot(jumpAudio.clip);
    }

    public void SetMode(CatMotorMode mode)
    {
        Mode = mode;
    }

    public float DashDuration(bool super)
    {
        return super ? constants.catSuperDashDuration : constants.catDashDuration;
    }

    public void StartDash(bool super)
    {
        currentDashSpeed = super ? constants.catSuperDashSpeed : constants.catDashSpeed;
        dashDirection = FacingRight ? 1f : -1f;
        Mode = CatMotorMode.Dash;
    }

    public void EndDash()
    {
        // A jump already ended the dash and carries its speed into the air
        if (Mode != CatMotorMode.Dash) return;

        Mode = CatMotorMode.Free;
        Vector2 v = body.linearVelocity;
        v.x = dashDirection * maxSpeed;
        body.linearVelocity = v;
    }

    // Knocked back by an obstacle: reverse direction (at least a minimum speed) and hop
    public void Bounce()
    {
        Vector2 v = body.linearVelocity;
        if (Mathf.Abs(v.x) < bounceMinSpeed)
        {
            float dir = Mathf.Abs(v.x) > 0.01f ? Mathf.Sign(v.x) : (FacingRight ? 1f : -1f);
            v.x = -dir * bounceMinSpeed;
        }
        else
        {
            v.x = -v.x;
        }
        v.y = 1.0f;
        airMomentumX = v.x;
        body.linearVelocity = v;
        onGround = false;
        Mode = CatMotorMode.Locked;
    }

    // Grab the wall the cat is touching: stop dead, no gravity, face the wall
    public void StartCling()
    {
        Mode = CatMotorMode.Cling;
        airMomentumX = 0f;
        body.linearVelocity = Vector2.zero;
        if (WallSide != 0 && sprite != null) sprite.flipX = WallSide < 0;
    }

    // Kick off the wall: away from it, with upward speed
    public void WallJump()
    {
        int away = WallSide != 0 ? -WallSide : (FacingRight ? -1 : 1);
        Vector2 v = new Vector2(away * constants.catWallJumpSpeedX, constants.catWallJumpSpeedY);
        Mode = CatMotorMode.Free;
        body.linearVelocity = v;
        airMomentumX = v.x;
        onGround = false;
        WallSide = 0;
        if (sprite != null) sprite.flipX = away < 0;
        if (jumpAudio != null && jumpAudio.clip != null) jumpAudio.PlayOneShot(jumpAudio.clip);   // same sound as a normal jump
    }

    public float WallJumpTime => constants.catWallJumpTime;

    // Back to the scene start pose (GameRestart / RoundReset)
    public void ResetState()
    {
        WallSide = 0;
        ClingKeyHeld = false;
        Mode = CatMotorMode.Free;
        AnimationOverride = null;
        SpeedMultiplier = 1f;
        jumpRequest = false;
        horizontalInput = 0f;
        airMomentumX = 0f;
        onGround = false;
        currentAnimation = null;

        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        if (sprite != null) sprite.flipX = initialFlipX;
        if (body != null)
        {
            body.position = initialPosition;
            body.rotation = initialRotation.eulerAngles.z;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        Physics2D.SyncTransforms();
    }

    // ------------------------------------------------ physics

    void Update()
    {
        if (onGround && (Mode == CatMotorMode.Free || Mode == CatMotorMode.Brake))
        {
            horizontalInput = Mode == CatMotorMode.Brake ? 0f : moveInput;
            if (moveInput > 0) sprite.flipX = false;
            else if (moveInput < 0) sprite.flipX = true;
        }

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        UpdateWallCollision();

        // Clinging to a wall: held in place until the state machine lets go
        if (Mode == CatMotorMode.Cling)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 v = body.linearVelocity;
        float dt = Time.fixedDeltaTime;
        float input = Mode == CatMotorMode.Locked ? 0f : horizontalInput;

        // Dash: fixed speed, no gravity. A jump cancels it and carries the dash speed into the air.
        if (Mode == CatMotorMode.Dash && !jumpRequest)
        {
            v.x = dashDirection * currentDashSpeed;
            body.linearVelocity = v;
            return;
        }

        if (jumpRequest)
        {
            bool fromDash = Mode == CatMotorMode.Dash;
            if (fromDash) Mode = CatMotorMode.Free;
            airMomentumX = JumpHorizontalSpeed(v.x, fromDash);
            v.y = jumpForce;
            onGround = false;
            jumpRequest = false;
        }
        else if (!onGround)
        {
            float g = gravity;
            if (v.y < 0f) g *= fallMultiplier;
            v.y -= g * dt;
        }
        else
        {
            v.y = 0f;
        }

        if (onGround)
        {
            float top = maxSpeed * SpeedMultiplier;
            float accel = moveSpeed * SpeedMultiplier;
            if (Mathf.Abs(input) > 0f)
            {
                bool sameDirection = Mathf.Sign(input) == Mathf.Sign(v.x) || Mathf.Abs(v.x) < 0.1f;
                if (sameDirection)
                {
                    v.x += input * accel * dt;
                    v.x = Mathf.Clamp(v.x, -top, top);
                }
                else
                {
                    v.x = Mathf.Lerp(v.x, 0f, groundFriction * dt);
                }
            }
            else
            {
                v.x = Mathf.Lerp(v.x, 0f, groundFriction * dt);
                if (Mathf.Abs(v.x) < 0.05f) v.x = 0f;
            }
        }
        else
        {
            v.x = airMomentumX;
        }

        body.linearVelocity = v;
    }

    // Horizontal speed the cat leaves the ground with. It follows the direction held on the move input:
    // nothing held is a straight jump. Going that way already faster than the move speed keeps the speed,
    // otherwise the cat leaves at the move speed. A jump out of a dash keeps the dash speed and direction.
    private float JumpHorizontalSpeed(float currentX, bool fromDash)
    {
        if (fromDash) return currentX;
        if (moveInput == 0) return 0f;

        float minSpeed = moveSpeed * SpeedMultiplier;
        float alongInput = currentX * moveInput;        // negative when moving against the held direction
        return moveInput * Mathf.Max(alongInput, minSpeed);
    }

    // Only a surface under the cat is ground (not the side of a Ground-tagged wall), and a wall the air
    // momentum pushes into stops that momentum so the cat cannot stick to it.
    void OnCollisionEnter2D(Collision2D col)
    {
        CheckContacts(col);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        CheckContacts(col);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(wallTag) && col.gameObject.TryGetComponent<PassableWhenGrounded>(out _)) WallSide = 0;

        if (col.gameObject.CompareTag(groundTag))
        {
            onGround = false;
            airMomentumX = body.linearVelocity.x;
        }
    }

    private void CheckContacts(Collision2D col)
    {
        bool standingOn = false;
        bool pushingIntoWall = false;

        // Only the outer side of a building wall can be clung to (not the map edge, not the inside)
        PassableWhenGrounded building = null;
        bool clingable = col.gameObject.CompareTag(wallTag) && col.gameObject.TryGetComponent(out building);
        for (int i = 0; i < col.contactCount; i++)
        {
            Vector2 normal = col.GetContact(i).normal;
            // The normal points from the wall towards the cat, so the wall is on the opposite side
            if (clingable && Mathf.Abs(normal.x) > 0.5f && normal.x * building.outwardX > 0f)
            {
                WallSide = normal.x > 0f ? -1 : 1;
            }
            if (normal.y > 0.5f) standingOn = true;
            if (Mathf.Abs(normal.x) > 0.5f && airMomentumX * normal.x < 0f) pushingIntoWall = true;
        }

        // Ignored while moving up: the floor contact is still reported on the step of a jump
        if (standingOn && col.gameObject.CompareTag(groundTag) && body.linearVelocity.y <= 0.1f)
        {
            onGround = true;
        }
        else if (!onGround && pushingIntoWall)
        {
            airMomentumX = 0f;
        }
    }

    // ------------------------------------------------ animation

    private void UpdateAnimation()
    {
        if (animator == null) return;

        string next;
        if (!string.IsNullOrEmpty(AnimationOverride))
        {
            next = AnimationOverride;
        }
        else if (!onGround)
        {
            float vy = body.linearVelocity.y;
            if (vy > 2.0f) next = ANIM_JUMP;
            else if (vy > 0f) next = ANIM_MID_JUMP;
            else if (vy > -2.0f) next = ANIM_MID_FALL;
            else next = ANIM_FALL;
        }
        else
        {
            float vx = body.linearVelocity.x;
            bool skidding = (vx > 0.5f && horizontalInput < -0.1f) ||
                            (vx < -0.5f && horizontalInput > 0.1f) ||
                            (vx != 0f && horizontalInput == 0f);
            if (skidding) next = ANIM_SKID;
            else if (Mathf.Abs(vx) > 0.2f) next = ANIM_RUN;
            else next = ANIM_IDLE;
        }

        if (currentAnimation != next)
        {
            animator.Play(next);
            currentAnimation = next;
        }
    }
}

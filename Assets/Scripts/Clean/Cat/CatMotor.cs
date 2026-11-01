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
    [Tooltip("Stairs are walkable like ground, solid only from above, and the cat can drop through them with Down.")]
    public string stairTag = "Stair";
    [Tooltip("The stairs stay switched off this long after Down is released, so the cat is clear of them.")]
    public float stairRestoreDelay = 0.2f;
    [Tooltip("Objects with this tag are only solid to the cat during a super dash.")]
    public string interactableTag = "Interactable";

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

    private float clingStartTime;
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

    // Slopes (stairs): the surface normal under the cat, so walking follows the slope
    private Vector2 groundNormal = Vector2.up;
    private bool groundIsStair = false;

    // Dropping through stairs while Down is held
    private readonly System.Collections.Generic.List<Collider2D> stairColliders = new System.Collections.Generic.List<Collider2D>();
    private bool downHeld = false;
    private bool ignoringStairs = false;
    private float stairRestoreTime = 0f;

    // Interactable objects (armor, bookshelf...): the cat passes through them, except in a super dash
    private readonly System.Collections.Generic.List<Collider2D> interactableColliders = new System.Collections.Generic.List<Collider2D>();
    private bool ignoringInteractables = false;
    private bool interactablesApplied = false;
    private bool currentDashIsSuper = false;
    private float nextInteractableScan = 0f;
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

    // ------------------------------------------------ interactable objects

    // The cat walks, runs, jumps and dashes straight through objects tagged Interactable; only a super dash
    // hits them.
    private void UpdateInteractableCollision()
    {
        bool ignore = !(Mode == CatMotorMode.Dash && currentDashIsSuper);
        bool rescan = Time.time >= nextInteractableScan;
        if (interactablesApplied && ignore == ignoringInteractables && !rescan) return;

        nextInteractableScan = Time.time + 2f;
        interactableColliders.Clear();
        try
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag(interactableTag))
            {
                interactableColliders.AddRange(go.GetComponents<Collider2D>());
            }
        }
        catch (UnityException)
        {
            // the tag does not exist in this project yet: nothing to do
        }
        foreach (Collider2D c in interactableColliders)
        {
            if (c != null && !c.isTrigger) Physics2D.IgnoreCollision(cachedCollider, c, ignore);
        }
        ignoringInteractables = ignore;
        interactablesApplied = true;
    }

    // ------------------------------------------------ dropping through stairs

    // Down pressed: the cat stops colliding with every stair, so it falls through the one it stands on
    public void OnDownPressed()
    {
        downHeld = true;
        if (ignoringStairs) return;

        stairColliders.Clear();
        foreach (GameObject stair in GameObject.FindGameObjectsWithTag(stairTag))
        {
            stairColliders.AddRange(stair.GetComponents<Collider2D>());
        }
        foreach (Collider2D c in stairColliders) Physics2D.IgnoreCollision(cachedCollider, c, true);
        ignoringStairs = true;

        if (groundIsStair)
        {
            // Standing on a stair: let go of it and fall
            onGround = false;
            groundIsStair = false;
            groundNormal = Vector2.up;
            airMomentumX = body.linearVelocity.x;
        }
    }

    public void OnDownReleased()
    {
        downHeld = false;
        stairRestoreTime = Time.time + stairRestoreDelay;
    }

    private void UpdateStairCollision()
    {
        if (!ignoringStairs || downHeld || Time.time < stairRestoreTime) return;

        foreach (Collider2D c in stairColliders)
        {
            if (c != null) Physics2D.IgnoreCollision(cachedCollider, c, false);
        }
        stairColliders.Clear();
        ignoringStairs = false;
    }

    // Vertical speed that keeps the cat on the slope it walks along (0 on flat ground)
    private float SlopeVelocityY(float vx)
    {
        if (groundNormal.y > 0.999f || groundNormal.y < 0.2f) return 0f;
        return -vx * groundNormal.x / groundNormal.y;
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
        currentDashIsSuper = super;
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
        // A dash that ran into something solid (e.g. a super dash into a bookshelf) stops there; only a
        // free dash leaves at running speed
        bool blocked = Mathf.Abs(v.x) < currentDashSpeed * 0.5f;
        if (!blocked) v.x = dashDirection * maxSpeed;
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

    // Thrown away from something that hurt the cat (in any state): away along x, and up
    public void Knockback(float awayX, float speedX, float speedY)
    {
        float dir = Mathf.Abs(awayX) > 0.01f ? Mathf.Sign(awayX) : (FacingRight ? -1f : 1f);
        Vector2 v = new Vector2(dir * speedX, speedY);
        airMomentumX = v.x;
        body.linearVelocity = v;
        onGround = false;
    }

    // Grab the wall the cat is touching: stop dead, no gravity, face the wall
    public void StartCling()
    {
        Mode = CatMotorMode.Cling;
        clingStartTime = Time.time;
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
        downHeld = false;
        stairRestoreTime = 0f;
        groundNormal = Vector2.up;
        groundIsStair = false;
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
        UpdateStairCollision();
        UpdateInteractableCollision();
        if (!onGround)
        {
            groundNormal = Vector2.up;
            groundIsStair = false;
        }

        // Clinging to a wall: held in place for a moment, then slides down slowly until the state machine lets go
        if (Mode == CatMotorMode.Cling)
        {
            bool sliding = Time.time - clingStartTime >= constants.catWallSlideDelay;
            body.linearVelocity = sliding ? new Vector2(0f, -constants.catWallSlideSpeed) : Vector2.zero;
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

        if (onGround) v.y = SlopeVelocityY(v.x);    // walk up and down a stair instead of into it

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

        if (col.gameObject.CompareTag(groundTag) || col.gameObject.CompareTag(stairTag))
        {
            // Walking off the top end of a slope: the upward speed that followed the slope is not a jump
            // (a jump has already cleared onGround), so drop it and carry on level
            if (onGround && !jumpRequest && body.linearVelocity.y > 0f)
            {
                Vector2 v = body.linearVelocity;
                v.y = 0f;
                body.linearVelocity = v;
            }
            onGround = false;
            airMomentumX = body.linearVelocity.x;
        }
    }

    private void CheckContacts(Collision2D col)
    {
        bool standingOn = false;
        bool pushingIntoWall = false;
        Vector2 standNormal = Vector2.zero;

        // Only building walls can be clung to (not the map edge), from the sides their marker allows
        PassableWhenGrounded building = null;
        bool clingable = col.gameObject.CompareTag(wallTag) && col.gameObject.TryGetComponent(out building);
        for (int i = 0; i < col.contactCount; i++)
        {
            Vector2 normal = col.GetContact(i).normal;
            // The normal points from the wall towards the cat, so the wall is on the opposite side
            if (clingable && Mathf.Abs(normal.x) > 0.5f && building.CanCling(normal.x))
            {
                WallSide = normal.x > 0f ? -1 : 1;
            }
            if (normal.y > 0.5f)
            {
                standingOn = true;
                if (normal.y > standNormal.y) standNormal = normal;     // the flattest surface under the cat
            }
            if (Mathf.Abs(normal.x) > 0.5f && airMomentumX * normal.x < 0f) pushingIntoWall = true;
        }

        bool isStair = col.gameObject.CompareTag(stairTag);
        bool isGround = col.gameObject.CompareTag(groundTag) || isStair;

        // Ignored while moving away from the surface: the floor contact is still reported on the step of a jump
        if (standingOn && isGround && Vector2.Dot(body.linearVelocity, standNormal) <= 0.1f)
        {
            onGround = true;
            groundNormal = standNormal;
            groundIsStair = isStair;
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

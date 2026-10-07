using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    public float moveSpeed = 10f;
    public float maxSpeed = 15f;
    public float jumpForce = 12f;

    [Header("Dash Settings")]
    public float dashSpeed = 25f;       
    public float dashDuration = 0.5f;   
    public float dashCooldown = 1.5f;   
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private float dashDirection = 1f;
    private bool isBouncing = false;
    public float bounceMinSpeed = 5f;

    [Header("Super Dash Settings")]
    public float superHoldTime = 0.5f;      // hold Left Shift this long, then release
    public float superDashSpeed = 40f;
    public float superDashDuration = 0.7f;
    private bool isHolding = false;
    private float holdTimer = 0f;
    private float holdStartTime = 0f;
    private ActionManager input;
    private int moveInput = 0;      // -1 / 0 / 1 from ActionManager.moveCheck
    private bool isSuperDashing = false;
    private float currentDashSpeed = 25f;

    // Public property so BreakableObject can read if player is dashing
    public bool IsDashing => isDashing;

    [Header("Slash Settings")]
    public float slashDuration = 0.35f;
    public float slashCooldown = 0.3f;
    private bool isSlashing = false;
    private float slashTimer = 0f;
    private float slashCooldownTimer = 0f;

    // Catching the prey only counts while slashing
    public bool IsSlashing => isSlashing;

    [Header("Custom Physics Settings")]
    public float gravity = 30f;
    public float fallMultiplier = 1.5f; 
    public float groundFriction = 15f;  

    [Header("Animation References")]
    private Animator animator;
    private string currentAnimationState;

    // Animation State Constants
    const string ANIM_IDLE = "cat-idle";
    const string ANIM_RUN = "cat-run";
    const string ANIM_JUMP = "cat-jump";
    const string ANIM_MID_JUMP = "cat-mid-jump";
    const string ANIM_MID_FALL = "cat-mid-fall";
    const string ANIM_FALL = "cat-fall";
    const string ANIM_SKID = "cat-skid";
    const string ANIM_DASH = "cat-dash";
    const string ANIM_SLASH = "cat-slash";
    const string ANIM_HOLD = "cat-hold";
    const string ANIM_SUPER_DASH = "cat-super-dash";

    // States
    public bool onGroundState = false;
    private bool jumpRequest = false;
    private float horizontalInput = 0f;
    private float airMomentumX = 0f; 

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private bool initialFlipX;

    private Rigidbody2D catBody;
    private SpriteRenderer catSprite;

    public AudioSource catAudio;

    void Start()
    {
        Application.targetFrameRate = 30;
        catBody = GetComponent<Rigidbody2D>();
        catSprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        catBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        catBody.gravityScale = 0f; // Handled strictly by code

        // Remember the scene start pose for restarts
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        initialFlipX = catSprite != null && catSprite.flipX;

        // Input comes from ActionManager (CatActions); subscribe to its one-shot events
        input = ActionManager.Instance;
        if (input == null)
        {
            Debug.LogError("PlayerMovement: no ActionManager in the scene, the cat cannot be controlled.", this);
            return;
        }
        moveInput = input.Move;
        input.jump.AddListener(HandleJumpPressed);
        input.slash.AddListener(HandleSlashPressed);
        input.dashPressed.AddListener(HandleDashPressed);
        input.dashReleased.AddListener(HandleDashReleased);
        input.moveCheck.AddListener(HandleMoveCheck);
    }

    void OnDestroy()
    {
        if (input == null) return;
        input.jump.RemoveListener(HandleJumpPressed);
        input.slash.RemoveListener(HandleSlashPressed);
        input.dashPressed.RemoveListener(HandleDashPressed);
        input.dashReleased.RemoveListener(HandleDashReleased);
        input.moveCheck.RemoveListener(HandleMoveCheck);
    }

    private void HandleMoveCheck(int direction)
    {
        moveInput = direction;
    }

    // ---------------------------------------------------------- input events

    private void HandleJumpPressed()
    {
        if (Time.timeScale == 0f) return;
        if (!onGroundState || isBouncing) return;

        jumpRequest = true;
        EndSlash();         // jumping interrupts the slash
        isHolding = false;  // ...and cancels a dash charge
        holdTimer = 0f;
    }

    // Slash does not touch velocity, so the cat keeps moving while slashing
    private void HandleSlashPressed()
    {
        if (Time.timeScale == 0f) return;
        if (isSlashing || isDashing || isHolding || slashCooldownTimer > 0f) return;

        isSlashing = true;
        slashTimer = slashDuration;
    }

    // Dash: hold to charge, release to dash. Held >= superHoldTime -> super dash, shorter -> normal dash.
    private void HandleDashPressed()
    {
        if (Time.timeScale == 0f) return;
        if (isHolding || !onGroundState || isDashing || dashCooldownTimer > 0f || isBouncing) return;

        EndSlash();
        isHolding = true;
        holdStartTime = Time.time;
        holdTimer = 0f;
    }

    private void HandleDashReleased()
    {
        if (!isHolding) return;

        bool super = Time.time - holdStartTime >= superHoldTime;
        isHolding = false;
        holdTimer = 0f;
        StartDash(super);
    }

    void Update()
    {
        if (input == null) return;

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (slashCooldownTimer > 0f)
        {
            slashCooldownTimer -= Time.deltaTime;
        }
        if (isSlashing)
        {
            slashTimer -= Time.deltaTime;
            if (slashTimer <= 0f) EndSlash();
        }

        float moveDir = moveInput;

        if (onGroundState && !isDashing && !isBouncing)
        {
            horizontalInput = isHolding ? 0f : moveDir;     // charging a dash brakes the cat (facing can still change)
            if (horizontalInput > 0) { catSprite.flipX = false; }
            else if (horizontalInput < 0) { catSprite.flipX = true; }
        }

        if (isHolding)
        {
            holdTimer = Time.time - holdStartTime;
            if (!onGroundState || isBouncing)
            {
                isHolding = false;      // walked off a ledge / bounced: charge is lost
                holdTimer = 0f;
            }
            else if (!input.DashHeld)
            {
                HandleDashReleased();   // safety: key already released
            }
        }

        UpdateAnimationState();
    }

    private void EndSlash()
    {
        if (!isSlashing) return;
        isSlashing = false;
        slashTimer = 0f;
        slashCooldownTimer = slashCooldown;
    }

    private void StartDash(bool super)
    {
        EndSlash();     // dashing interrupts the slash
        isDashing = true;
        isSuperDashing = super;
        currentDashSpeed = super ? superDashSpeed : dashSpeed;
        dashTimer = super ? superDashDuration : dashDuration;
        dashCooldownTimer = dashCooldown;
        dashDirection = catSprite.flipX ? -1f : 1f;

        if (catAudio != null && catAudio.clip != null)
        {
            catAudio.PlayOneShot(catAudio.clip);
        }
    }

    void FixedUpdate()
    {
        Vector2 currentVel = catBody.linearVelocity;

        // Handle Obstacle Bounce
        if (isBouncing)
        {
            isDashing = false;
            dashTimer = 0f;

            if (Mathf.Abs(currentVel.x) < bounceMinSpeed)
            {
                // Too slow: push back at a fixed speed, opposite to movement (or facing if standing still)
                float dir = Mathf.Abs(currentVel.x) > 0.01f
                    ? Mathf.Sign(currentVel.x)
                    : (catSprite.flipX ? -1f : 1f);
                currentVel.x = -dir * bounceMinSpeed;
            }
            else
            {
                currentVel.x = -currentVel.x;
            }
            currentVel.y = 1.0f;
            airMomentumX = currentVel.x;

            catBody.linearVelocity = currentVel;

            isBouncing = false; 
            return;
        }

        // Handle Dash Momentum
        // A jump request cancels the dash and falls through to the jump logic below,
        // carrying the dash speed into the air via airMomentumX.
        if (isDashing && !jumpRequest)
        {
            dashTimer -= Time.fixedDeltaTime;
            currentVel.x = dashDirection * currentDashSpeed;

            if (dashTimer <= 0f)
            {
                isDashing = false;
                currentVel.x = dashDirection * maxSpeed;
            }

            catBody.linearVelocity = currentVel;
            return; 
        }

        // Vertical Custom Gravity / Jumping
        if (jumpRequest)
        {
            isDashing = false;
            dashTimer = 0f;
            airMomentumX = currentVel.x; 
            currentVel.y = jumpForce;
            onGroundState = false;
            jumpRequest = false;
        }
        else if (!onGroundState)
        {
            float appliedGravity = gravity;
            if (currentVel.y < 0)
            {
                appliedGravity *= fallMultiplier; 
            }

            currentVel.y -= appliedGravity * Time.fixedDeltaTime;
        }
        else
        {
            currentVel.y = 0f;
        }

        // Horizontal Movement / Friction
        if (onGroundState)
        {
            if (Mathf.Abs(horizontalInput) > 0)
            {
                bool sameDirection = (Mathf.Sign(horizontalInput) == Mathf.Sign(currentVel.x)) || (Mathf.Abs(currentVel.x) < 0.1f);

                if (sameDirection)
                {
                    currentVel.x += horizontalInput * moveSpeed * Time.fixedDeltaTime;
                    currentVel.x = Mathf.Clamp(currentVel.x, -maxSpeed, maxSpeed);
                }
                else
                {
                    currentVel.x = Mathf.Lerp(currentVel.x, 0f, groundFriction * Time.fixedDeltaTime);
                }
            }
            else
            {
                currentVel.x = Mathf.Lerp(currentVel.x, 0f, groundFriction * Time.fixedDeltaTime);
                if (Mathf.Abs(currentVel.x) < 0.05f) currentVel.x = 0f;
            }
        }
        else
        {
            currentVel.x = airMomentumX;
        }

        catBody.linearVelocity = currentVel;
    }

    private void UpdateAnimationState()
    {
        if (animator == null) return;

        string newState;

        if (!onGroundState)
        {
            if (catBody.linearVelocity.y > 2.0f) 
            {
                newState = ANIM_JUMP;
            }
            else if (catBody.linearVelocity.y > 0f)
            {
                newState = ANIM_MID_JUMP;
            }
            else if (catBody.linearVelocity.y > -2.0f)
            {
                newState = ANIM_MID_FALL;
            }
            else
            {
                newState = ANIM_FALL;
            }
        }
        else
        {
            bool isSkidding = (catBody.linearVelocity.x > 0.5f && horizontalInput < -0.1f) || 
                              (catBody.linearVelocity.x < -0.5f && horizontalInput > 0.1f) ||
                              (catBody.linearVelocity.x != 0f && horizontalInput == 0f);

            if (isDashing)
            {
                newState = isSuperDashing ? ANIM_SUPER_DASH : ANIM_DASH;
            }
            else if (isHolding)
            {
                newState = ANIM_HOLD;
            }
            else if (isSkidding)
            {
                newState = ANIM_SKID;
            }
            else if (Mathf.Abs(catBody.linearVelocity.x) > 0.2f)
            {
                newState = ANIM_RUN;
            }
            else
            {
                newState = ANIM_IDLE;
            }
        }

        if (isSlashing) newState = ANIM_SLASH;   // slash overrides the movement animation

        if (currentAnimationState != newState)
        {
            animator.Play(newState);
            currentAnimationState = newState;
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            onGroundState = true;
        }
        else
        {
            TryCatchPrey(col);
        }
    }

    // Also covers starting a slash while already touching the prey
    void OnCollisionStay2D(Collision2D col)
    {
        TryCatchPrey(col);
    }

    private void TryCatchPrey(Collision2D col)
    {
        if (isSlashing && col.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Prey slashed!");
            GameManager.Instance.PreyCaught();
        }
    }

    // Called by Obstacle.cs
    public void Bounce()
    {
        isBouncing = true;
    }

    // Called by GameManager on restart
    public void ResetState()
    {
        isSlashing = false;
        slashTimer = 0f;
        slashCooldownTimer = 0f;
        isHolding = false;
        holdTimer = 0f;
        isSuperDashing = false;
        isDashing = false;
        isBouncing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        jumpRequest = false;
        horizontalInput = 0f;
        airMomentumX = 0f;
        onGroundState = true;
        currentAnimationState = null;

        // Back to the scene start pose
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        if (catSprite != null) catSprite.flipX = initialFlipX;
        if (catBody != null)
        {
            catBody.position = initialPosition;
            catBody.rotation = initialRotation.eulerAngles.z;
            catBody.linearVelocity = Vector2.zero;
            catBody.angularVelocity = 0f;
        }
        Physics2D.SyncTransforms();
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            onGroundState = false;
            airMomentumX = catBody.linearVelocity.x;
        }
    }

    public void RestartButtonCallback(int input)
    {
        GameManager.Instance.RestartButtonCallback(input);
    }
}
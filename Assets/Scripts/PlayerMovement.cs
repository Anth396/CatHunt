using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 

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

    // States
    public bool onGroundState = false;
    private bool jumpRequest = false;
    private float horizontalInput = 0f;
    private float airMomentumX = 0f; 

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
    }

    void Update()
    {
        if (Keyboard.current == null) return;

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

        float moveDir = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDir = 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDir = -1f;

        if (onGroundState && !isDashing && !isBouncing)
        {
            horizontalInput = moveDir;
            if (horizontalInput > 0) { catSprite.flipX = false; }
            else if (horizontalInput < 0) { catSprite.flipX = true; }
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState && !isBouncing)
        {
            jumpRequest = true;
            EndSlash();     // jumping interrupts the slash
        }

        // Slash (J): does not touch velocity, so the cat keeps moving while slashing
        if (Keyboard.current.jKey.wasPressedThisFrame && !isSlashing && !isDashing && slashCooldownTimer <= 0f)
        {
            isSlashing = true;
            slashTimer = slashDuration;
        }

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame && onGroundState && !isDashing && dashCooldownTimer <= 0f && !isBouncing)
        {
            StartDash();
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

    private void StartDash()
    {
        EndSlash();     // dashing interrupts the slash
        isDashing = true;
        dashTimer = dashDuration;
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
            currentVel.x = dashDirection * dashSpeed;

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
                newState = ANIM_DASH;
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
            GameManager.Instance.StopGameAndShowWin(GameManager.Instance.timer);
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
        isDashing = false;
        isBouncing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        jumpRequest = false;
        horizontalInput = 0f;
        airMomentumX = 0f;
        onGroundState = true;
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
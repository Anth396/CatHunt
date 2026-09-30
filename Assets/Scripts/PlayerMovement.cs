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

    [Header("Custom Physics Settings")]
    public float gravity = 30f;
    public float fallMultiplier = 1.5f; 
    public float groundFriction = 15f;  

    [Header("Animation References")]
    private Animator animator;
    private string currentAnimationState;

    // Animation State Constants (Match exact names of your Animation states/clips)
    const string ANIM_IDLE = "cat-idle";
    const string ANIM_RUN = "cat-run";
    const string ANIM_JUMP = "cat-jump";
    const string ANIM_SKID = "cat-skid";
    const string ANIM_DASH = "cat-dash";

    // States
    private bool onGroundState = true;
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
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        float moveDir = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDir = 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDir = -1f;

        if (onGroundState && !isDashing)
        {
            horizontalInput = moveDir;
            
            if (horizontalInput > 0) { catSprite.flipX = false; }
            else if (horizontalInput < 0) { catSprite.flipX = true; }
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState && !isDashing)
        {
            jumpRequest = true;
        }

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame && onGroundState && !isDashing && dashCooldownTimer <= 0f)
        {
            StartDash();
        }

        // Handle animation selection logic every frame update
        UpdateAnimationState();
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        dashDirection = catSprite.flipX ? -1f : 1f;
    }

    void FixedUpdate()
    {
        Vector2 currentVel = catBody.linearVelocity;

        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            currentVel.x = dashDirection * dashSpeed;
            currentVel.y = 0f; 

            if (dashTimer <= 0f)
            {
                isDashing = false;
                currentVel.x = dashDirection * maxSpeed;
            }

            catBody.linearVelocity = currentVel;
            return; 
        }

        if (jumpRequest)
        {
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
            currentVel.y = Mathf.Min(currentVel.y, 0f);
        }

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

        // 1. If Cat's off the ground -> play jump clip
        if (!onGroundState)
        {
            newState = ANIM_JUMP;
        }
        else
        {
            // Check for Skid condition: 
            // Skidding happens when physical velocity is moving significantly in one direction, 
            // but player input is actively trying to push the opposite way.
            bool isSkidding = (catBody.linearVelocity.x > 0.5f && horizontalInput < -0.1f) || 
                              (catBody.linearVelocity.x < -0.5f && horizontalInput > 0.1f) ||
                              (catBody.linearVelocity.x != 0f && horizontalInput == 0f);

            if (isSkidding)
            {
                newState = ANIM_SKID;
            }
            else if (isDashing)
            {
                newState = ANIM_DASH;
            }
            // 2. If Cat's moving (has velocity magnitude above threshold) -> play run clip looped
            else if (Mathf.Abs(catBody.linearVelocity.x) > 0.2f)
            {
                newState = ANIM_RUN;
            }
            // 4. Otherwise -> stay at idle clip
            else
            {
                newState = ANIM_IDLE;
            }
        }

        // Prevent restarting the same animation over and over every frame
        if (currentAnimationState != newState)
        {
            animator.Play(newState);
            currentAnimationState = newState;
        }
    }

    void PlayDashSound()
    {
        // play jump sound
        catAudio.PlayOneShot(catAudio.clip);
    }
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            onGroundState = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Collided with prey!");
            GameManager.Instance.StopGameAndShowWin(GameManager.Instance.timer);
        }
    }

    public void RestartButtonCallback(int input)
    {
        GameManager.Instance.RestartButtonCallback(input);
    }
}
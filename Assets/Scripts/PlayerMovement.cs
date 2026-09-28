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
    public float dashSpeed = 25f;       // Exceeds maxSpeed during dash
    public float dashDuration = 0.2f;   // How long the dash lasts
    public float dashCooldown = 5.0f;   // Cooldown time between dashes
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private float dashDirection = 1f;

    [Header("Custom Physics Settings")]
    public float gravity = 30f;
    public float fallMultiplier = 1.5f; 
    public float groundFriction = 15f;  

    // States
    private bool onGroundState = true;
    private bool jumpRequest = false;
    private float horizontalInput = 0f;
    private float airMomentumX = 0f; 

    private Rigidbody2D catBody;
    private SpriteRenderer catSprite;

    void Start()
    {
        Application.targetFrameRate = 30;
        catBody = GetComponent<Rigidbody2D>();
        catSprite = GetComponent<SpriteRenderer>();
        catBody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    // 1. UPDATE: Handles inputs only
    void Update()
    {
        if (Keyboard.current == null) return;

        // Decrease cooldown timer continuously
        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // Capture Horizontal Input keys (A/D or Left/Right)
        float moveDir = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDir = 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDir = -1f;

        if (onGroundState && !isDashing)
        {
            horizontalInput = moveDir;
            
            // Handle Sprite Flipping safely on ground
            if (horizontalInput > 0) { catSprite.flipX = false; }
            else if (horizontalInput < 0) { catSprite.flipX = true; }
        }

        // Capture Jump input trigger
        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState && !isDashing)
        {
            jumpRequest = true;
        }

        // Capture Dash input trigger (e.g., Left Shift key)
        // Rule: Can only be cast on the ground, and not while already dashing or cooling down
        if (Keyboard.current.leftShiftKey.wasPressedThisFrame && onGroundState && !isDashing && dashCooldownTimer <= 0f)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        // Dash follows the direction the player is currently facing (based on SpriteRenderer flipX)
        dashDirection = catSprite.flipX ? -1f : 1f;
    }

    // 2. FIXEDUPDATE: Handles all physical calculations, velocities, and reactions
    void FixedUpdate()
    {
        Vector2 currentVel = catBody.linearVelocity;

        // --- A. DASH PHYSICS HANDLER ---
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            
            // Force dash velocity in the direction faced
            currentVel.x = dashDirection * dashSpeed;
            currentVel.y = 0f; // Keep ground-dash locked flat

            if (dashTimer <= 0f)
            {
                // End dash: Instantly drop back down to maximum speed limit matching travel direction
                isDashing = false;
                currentVel.x = dashDirection * maxSpeed;
            }

            catBody.linearVelocity = currentVel;
            return; // Skip normal movement calculations while dash is active
        }

        // --- B. VERTICAL PHYSICS (Gravity, Jumping, Apex Slowdown, Fall Acceleration) ---
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

        // --- C. HORIZONTAL PHYSICS (Movement, Air Lockout, Ground Friction, and Dash Post-Behavior) ---
        if (onGroundState)
        {
            if (Mathf.Abs(horizontalInput) > 0)
            {
                // Check if current movement key matches the direction the player was traveling/dashing
                bool sameDirection = (Mathf.Sign(horizontalInput) == Mathf.Sign(currentVel.x)) || (Mathf.Abs(currentVel.x) < 0.1f);

                if (sameDirection)
                {
                    // Retain/accelerate speed normally
                    currentVel.x += horizontalInput * moveSpeed * Time.fixedDeltaTime;
                    currentVel.x = Mathf.Clamp(currentVel.x, -maxSpeed, maxSpeed);
                }
                else
                {
                    // If movement key is opposite to direction, gradually slow down using friction
                    currentVel.x = Mathf.Lerp(currentVel.x, 0f, groundFriction * Time.fixedDeltaTime);
                }
            }
            else
            {
                // Apply ground friction when keys are lifted
                currentVel.x = Mathf.Lerp(currentVel.x, 0f, groundFriction * Time.fixedDeltaTime);
                if (Mathf.Abs(currentVel.x) < 0.05f) currentVel.x = 0f;
            }
        }
        else
        {
            // Airborne momentum lock
            currentVel.x = airMomentumX;
        }

        catBody.linearVelocity = currentVel;
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
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

    [Header("Custom Physics Settings")]
    public float gravity = 30f;
    public float fallMultiplier = 1.5f; 
    public float groundFriction = 15f;  

    // States
    private bool onGroundState = true;
    private bool jumpRequest = false;
    private float horizontalInput = 0f;
    private float airMomentumX = 0f; // Stores exact horizontal momentum when leaving the ground

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

        // Capture Horizontal Input keys (A/D or Left/Right)
        float moveDir = 0f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDir = 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDir = -1f;

        // Ground vs Air behavior for inputs
        if (onGroundState)
        {
            horizontalInput = moveDir;
            
            // Handle Sprite Flipping safely on ground
            if (horizontalInput > 0) { catSprite.flipX = false; }
            else if (horizontalInput < 0) { catSprite.flipX = true; }
        }
        // Note: Horizontal inputs are ignored mid-air to prevent mid-air steering.

        // Capture Jump input trigger
        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState)
        {
            jumpRequest = true;
        }
    }

    // 2. FIXEDUPDATE: Handles all physical calculations, velocities, and reactions
    void FixedUpdate()
    {
        Vector2 currentVel = catBody.linearVelocity;

        // --- A. VERTICAL PHYSICS (Gravity, Jumping, Apex Slowdown, Fall Acceleration) ---
        if (jumpRequest)
        {
            // FIX: Preserve exact current horizontal velocity instead of overriding or scaling it
            airMomentumX = currentVel.x; 

            currentVel.y = jumpForce;
            onGroundState = false;
            jumpRequest = false;
        }
        else if (!onGroundState)
        {
            // Custom Gravity logic
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

        // --- B. HORIZONTAL PHYSICS (Movement, Air Lockout, and Ground Friction) ---
        if (onGroundState)
        {
            if (Mathf.Abs(horizontalInput) > 0)
            {
                // Accelerate horizontally based on input
                currentVel.x += horizontalInput * moveSpeed * Time.fixedDeltaTime;
                currentVel.x = Mathf.Clamp(currentVel.x, -maxSpeed, maxSpeed);
            }
            else
            {
                // Apply ground friction only when keys are lifted
                currentVel.x = Mathf.Lerp(currentVel.x, 0f, groundFriction * Time.fixedDeltaTime);
                if (Mathf.Abs(currentVel.x) < 0.05f) currentVel.x = 0f;
            }
        }
        else
        {
            // Airborne: Lock the horizontal velocity completely to what it was when leaving the ground, 
            // ensuring jumping never warps or changes your linear velocity.
            currentVel.x = airMomentumX;
        }

        // Apply final custom calculated velocity back to Rigidbody2D
        catBody.linearVelocity = currentVel;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground"))
        {
            onGroundState = true;
            
            // FIX: If we land while actively holding a movement key that matches our current sliding direction,
            // immediately transition that momentum back into active ground movement input without a speed hitch.
            if (Mathf.Abs(horizontalInput) > 0)
            {
                // Ensures smooth handoff from air momentum to ground acceleration
                // If sliding left (-), but pressing right (+), it will naturally brake/pivot instead of stuttering.
            }
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
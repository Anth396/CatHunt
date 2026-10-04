using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prey AI (the rat). No dash. Uses the same custom physics as PlayerMovement.
///
/// Actions : run, walk, wait, turn, short/full jump (early/late), drop.
/// Decision: utility scoring (distance from cat, time to land, fork count, verticality, bait value)
///           with a 2-step lookahead over a short simulation of the rat and the cat's predicted path.
/// Rules   : must turn when it touches a wall; never walks off a void edge; jumps obstacles.
///
/// Split over partial files: Sensing (scans, cat model), Brain (decisions, execution), Sim (scoring).
/// </summary>
public partial class EnemyMovement : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("BoxCollider2D used to detect the player. Assign it in the Inspector / at runtime.")]
    public BoxCollider2D detectBox;

    [Header("Movement")]
    public float acceleration = 30f;    // ground acceleration
    public float maxSpeed = 15f;         // run speed
    public float walkSpeed = 5f;
    public float jumpForce = 15f;       // full jump take-off speed
    public float shortJumpFraction = 0.6f;
    public float airMinSpeed = 5f;      // minimum horizontal speed when leaving the ground

    [Header("Custom Physics Settings")]
    public float gravity = 30f;
    public float fallMultiplier = 1.5f;
    public float groundFriction = 15f;

    [Header("Sensing")]
    public float wallCheckDistance = 0.3f;
    public float obstacleCheckDistance = 4.0f;
    public float minObstacleHeight = 0.1f;
    public float jumpClearance = 0.6f;
    public float jumpTimingMargin = 0.05f;
    public float stackCheckHeight = 0.15f;
    public int maxStackLevels = 4;
    public float ledgeScanRange = 6f;
    public float ledgeMinDepth = 0.6f;
    public float maxDropDepth = 8f;
    public string groundTag = "Ground";
    public string wallTag = "Ground";
    public string playerTag = "Player";

    [Header("AI - Decision")]
    public float minActionTime = 0.35f;
    public float maxActionTime = 0.8f;
    public float minDecisionGap = 0.12f;
    public float safeGap = 10f;         // gap at which distance score saturates
    public float dangerGap = 2.5f;
    public float baitGap = 5f;
    public float airRef = 0.9f;         // airtime that counts as "fully exposed"
    public float verticalRange = 3f;

    [Header("AI - Weights")]
    public float wDistance = 1.0f;
    public float wTimeToLand = 0.6f;
    public float wForks = 0.35f;
    public float wVertical = 0.25f;
    public float wBait = 0.7f;
    public float wLookahead = 0.6f;
    public float noise = 0.12f;
    public float caughtPenalty = 4f;
    public float blockedPenalty = 1.5f;
    public float clipPenalty = 2f;
    public float turnCost = 0.1f;
    public float waitCost = 0.1f;
    public float chainBonus = 0.4f;
    public float chainWindow = 0.35f;

    [Header("AI - Variation")]
    public float speedVariation = 0.2f;         // +/- fraction of run/walk speed per action
    public float jumpHeightVariation = 0.15f;   // +/- fraction of jump height
    public float timingVariation = 0.1f;        // +/- jitter on the early/late timing
    public float earlyAlpha = 0.85f;            // 0 = latest safe take-off, 1 = earliest safe
    public float lateAlpha = 0.15f;
    public float freeLateDelay = 0.3f;          // late jump with no target waits this long
    public float lateTriggerGap = 3f;           // ...or until the cat is this close
    public float forkMinOpen = 2.5f;
    public float stuckTime = 1.0f;

    [Header("Start")]
    public int startDirection = 1;      // 1 = right, -1 = left
    public Vector3 startPosition = new Vector3(0.0f, 0.0f, 0.0f);

    [Header("Animation")]
    public bool spriteFacesRight = true;

    [Header("Debug")]
    public string debugMove;

    // Animation state names
    const string ANIM_IDLE = "rat-idle";
    const string ANIM_RUN = "rat-run";
    const string ANIM_JUMP = "rat-jump";
    const string ANIM_MID_AIR = "rat-mid-air";
    const string ANIM_FALL = "rat-fall";

    private Rigidbody2D enemyBody;
    private SpriteRenderer enemySprite;
    private Animator animator;
    private Collider2D bodyCollider;
    private string currentAnimationState;

    // Physics state
    private bool onGroundState = false;
    private float moveDir = 1f;
    private float airMomentumX = 0f;
    private bool jumpRequest = false;
    private float pendingJumpSpeed = 0f;
    private bool jumpedThisAir = false;
    private float targetSpeed = 0f;
    private float ratWidth = 1f;
    private float ratHeight = 1f;

    // Player awareness
    private PlayerMovement player;
    private Collider2D playerCollider;
    private Rigidbody2D playerBody;
    private bool playerDetected = false;

    private ContactFilter2D solidFilter;    // ignores triggers
    private ContactFilter2D anyFilter;      // includes triggers
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[6];
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly List<Collider2D> stackResults = new List<Collider2D>();

    void Start()
    {
        enemyBody = GetComponent<Rigidbody2D>();
        enemySprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        enemyBody.bodyType = RigidbodyType2D.Dynamic;
        enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        enemyBody.gravityScale = 0f; // Handled strictly by code

        // Pick the collider that is the rat's body (not the detect box)
        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            if (col == detectBox) continue;
            if (bodyCollider == null || (bodyCollider.isTrigger && !col.isTrigger)) bodyCollider = col;
        }
        if (bodyCollider == null)
        {
            Debug.LogWarning("EnemyMovement: no body Collider2D found on " + name, this);
        }

        solidFilter = ContactFilter2D.noFilter;
        solidFilter.useTriggers = false;

        anyFilter = ContactFilter2D.noFilter;
        anyFilter.useTriggers = true;

        moveDir = startDirection >= 0 ? 1f : -1f;
        InitBrain();
    }

    void Update()
    {
        UpdateAnimationState();
    }

    void FixedUpdate()
    {
        if (enemyBody == null || bodyCollider == null) return;

        float dt = Time.fixedDeltaTime;
        Vector2 currentVel = enemyBody.linearVelocity;
        ratWidth = bodyCollider.bounds.size.x;
        ratHeight = bodyCollider.bounds.size.y;

        DetectPlayer();
        BuildCat();
        timeSinceLanding += dt;

        if (onGroundState)
        {
            DirScan fwd = ScanDirection(moveDir, true);
            RunBrain(currentVel, fwd, dt);
        }
        else if (Mathf.Abs(airMomentumX) > 0.01f)
        {
            // In the air: stop pushing into a wall
            DirScan wallScan = ScanDirection(Mathf.Sign(airMomentumX), false);
            if (wallScan.wall && wallScan.wallDist <= wallCheckDistance) airMomentumX = 0f;
        }

        // ---- Vertical: custom gravity / jumping ----
        if (jumpRequest && onGroundState)
        {
            airMomentumX = moveDir * Mathf.Max(Mathf.Abs(currentVel.x), airMinSpeed);
            currentVel.y = pendingJumpSpeed > 0f ? pendingJumpSpeed : jumpForce;
            onGroundState = false;
            jumpedThisAir = true;
        }
        else if (!onGroundState)
        {
            float appliedGravity = gravity;
            if (currentVel.y < 0) appliedGravity *= fallMultiplier;
            currentVel.y -= appliedGravity * dt;
        }
        else
        {
            currentVel.y = 0f;
        }
        jumpRequest = false;

        // ---- Horizontal: acceleration / friction (ground) or frozen momentum (air) ----
        if (onGroundState)
        {
            float along = currentVel.x * moveDir;
            float sp = targetSpeed;
            if (sp <= 0.01f || along < -0.1f) along = Mathf.Lerp(along, 0f, groundFriction * dt);
            else if (along < sp) along = Mathf.Min(along + acceleration * dt, sp);
            else along = Mathf.Lerp(along, sp, groundFriction * dt);

            if (sp <= 0.01f && Mathf.Abs(along) < 0.05f) along = 0f;
            currentVel.x = along * moveDir;
        }
        else
        {
            currentVel.x = airMomentumX;
        }

        enemyBody.linearVelocity = currentVel;
    }

    // Ground state is driven by collisions, same as PlayerMovement
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(groundTag))
        {
            bool wasAirborne = !onGroundState;
            onGroundState = true;
            jumpedThisAir = false;
            if (wasAirborne) OnLanded();
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.gameObject.CompareTag(groundTag))
        {
            onGroundState = false;
            // Walked off a ledge: keep horizontal speed. After a jump the take-off speed must stay,
            // otherwise this overwrites it with the (slower) ground speed and the jump falls short.
            if (!jumpedThisAir) airMomentumX = enemyBody.linearVelocity.x;
        }
    }

    private void UpdateAnimationState()
    {
        if (enemyBody == null) return;

        Vector2 vel = enemyBody.linearVelocity;

        if (enemySprite != null && Mathf.Abs(moveDir) > 0f)
        {
            bool faceLeft = moveDir < 0f;
            enemySprite.flipX = spriteFacesRight ? faceLeft : !faceLeft;
        }

        if (animator == null) return;

        string newState;
        if (!onGroundState)
        {
            if (vel.y > 2.0f) newState = ANIM_JUMP;
            else if (vel.y > -2.0f) newState = ANIM_MID_AIR;
            else newState = ANIM_FALL;
        }
        else if (Mathf.Abs(vel.x) > 0.2f)
        {
            newState = ANIM_RUN;
        }
        else
        {
            newState = ANIM_IDLE;
        }

        if (currentAnimationState != newState)
        {
            animator.Play(newState);
            currentAnimationState = newState;
        }
    }

    // Called by GameManager on restart
    public void ResetState()
    {
        if (enemyBody != null) enemyBody.linearVelocity = Vector2.zero;
        airMomentumX = 0f;
        jumpRequest = false;
        pendingJumpSpeed = 0f;
        jumpedThisAir = false;
        playerDetected = false;
        moveDir = startDirection >= 0 ? 1f : -1f;
        InitBrain();
    }
}

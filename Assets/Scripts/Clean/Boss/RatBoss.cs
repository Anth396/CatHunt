using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The rat boss of the cellar. It walks along the floor toward the cat and bites it when close. It loses a heart
/// when the cat hits it with a super dash, or when a falling chandelier lands on it; after each hit it is
/// immune for a moment. With no hearts left it falls over and onDefeated is raised.
/// Uses its own simple physics: the speed is set by code, gravity comes from the Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class RatBoss : MonoBehaviour
{
    public enum State { Chase, Biting, Hurt, Dead }

    [Header("Hearts")]
    public int maxHearts = 3;
    [Tooltip("After a hit the boss cannot be hurt again for this long.")]
    public float invulnerableTime = 1.5f;
    [Tooltip("How long the take-damage pose is held (the boss does not move meanwhile).")]
    public float hurtTime = 0.6f;

    [Header("Movement")]
    public float walkSpeed = 3f;
    [Tooltip("The boss stays between these x positions.")]
    public float arenaMinX = -13f;
    public float arenaMaxX = 13f;
    [Tooltip("It stops this far from the cat (centre to centre) instead of pushing against it.")]
    public float stopDistance = 2f;
    public bool spriteFacesRight = true;

    [Header("Bite")]
    public Vector2 biteRange = new Vector2(2.8f, 1.8f);
    [Tooltip("Time from the start of the bite to the moment it lands.")]
    public float biteWindup = 0.5f;
    public float biteDuration = 0.9f;
    public float biteCooldown = 1.0f;
    public AudioSource biteAudio;

    [Header("Scene")]
    public Transform cat;
    public CatHealth catHealth;
    [Tooltip("Raised when the cat's super dash hits (the cat's state machine answers with the knock back).")]
    public SimpleGameEvent obstacleHit;
    [Tooltip("Filled with every FallingObject in the scene when empty.")]
    public List<FallingObject> hazards = new List<FallingObject>();

    [Header("Events")]
    public UnityEvent onHeartsChanged = new UnityEvent();
    public UnityEvent onDefeated = new UnityEvent();

    const string ANIM_IDLE = "rat-boss-idle";
    const string ANIM_WALK = "rat-boss-walk";
    const string ANIM_BITE = "rat-boss-bite";
    const string ANIM_HURT = "rat-boss-take-damage";
    const string ANIM_DEAD = "rat-boss-dead";

    public int Hearts { get; private set; }
    public State Current { get; private set; } = State.Chase;

    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer sprite;
    private Animator animator;
    private string currentAnim;
    private bool facingRight = true;

    private float stateTimer;
    private float invulnerableTimer;
    private float biteCooldownTimer;
    private bool biteLanded;

    private Vector3 startPosition;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        startPosition = transform.position;
        Hearts = maxHearts;
    }

    void Start()
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bodyCollider.sharedMaterial = new PhysicsMaterial2D("RatBoss") { friction = 0f, bounciness = 0f };

        if (cat == null)
        {
            CatStateController c = FindFirstObjectByType<CatStateController>();
            if (c != null) cat = c.transform;
        }
        if (catHealth == null && cat != null) catHealth = cat.GetComponent<CatHealth>();
        if (hazards.Count == 0) hazards.AddRange(FindObjectsByType<FallingObject>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        // Flat floor: stair pieces and the cat-only building walls are not in its way
        foreach (Collider2D c in FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.isTrigger) continue;
            if (c.CompareTag("Stair") || c.GetComponent<PassableWhenGrounded>() != null)
                Physics2D.IgnoreCollision(bodyCollider, c, true);
        }

        Play(ANIM_IDLE);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (invulnerableTimer > 0f) invulnerableTimer -= dt;
        if (biteCooldownTimer > 0f) biteCooldownTimer -= dt;

        if (Current == State.Dead) return;

        CheckHazards();
        if (Current == State.Dead) return;

        switch (Current)
        {
            case State.Chase: Chase(); break;
            case State.Biting: Bite(dt); break;
            case State.Hurt: Hurt(dt); break;
        }
    }

    // ------------------------------------------------------------------ behaviour

    private void Chase()
    {
        if (cat == null) { Stop(); Play(ANIM_IDLE); return; }

        float dx = cat.position.x - transform.position.x;
        float dy = Mathf.Abs(cat.position.y - transform.position.y);
        Face(dx);

        if (biteCooldownTimer <= 0f && Mathf.Abs(dx) <= biteRange.x && dy <= biteRange.y && catHealth != null && !catHealth.IsDead)
        {
            Current = State.Biting;
            stateTimer = 0f;
            biteLanded = false;
            Stop();
            Play(ANIM_BITE);
            if (biteAudio != null && biteAudio.clip != null) biteAudio.PlayOneShot(biteAudio.clip);
            return;
        }

        float x = transform.position.x;
        bool blocked = (dx > 0f && x >= arenaMaxX) || (dx < 0f && x <= arenaMinX);
        if (Mathf.Abs(dx) > stopDistance && !blocked)
        {
            body.linearVelocity = new Vector2(Mathf.Sign(dx) * walkSpeed, body.linearVelocity.y);
            Play(ANIM_WALK);
        }
        else
        {
            Stop();
            Play(ANIM_IDLE);
        }
    }

    private void Bite(float dt)
    {
        Stop();
        stateTimer += dt;

        if (!biteLanded && stateTimer >= biteWindup)
        {
            biteLanded = true;
            // The bite lands only if the cat is still close
            if (cat != null && catHealth != null)
            {
                Vector2 d = cat.position - transform.position;
                if (Mathf.Abs(d.x) <= biteRange.x + 0.5f && Mathf.Abs(d.y) <= biteRange.y) catHealth.TakeHit(transform.position);
            }
        }

        if (stateTimer >= biteDuration)
        {
            Current = State.Chase;
            biteCooldownTimer = biteCooldown;
        }
    }

    private void Hurt(float dt)
    {
        Stop();
        stateTimer += dt;
        if (stateTimer >= hurtTime) Current = State.Chase;
    }

    private void Stop()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void Face(float dx)
    {
        if (Mathf.Abs(dx) < 0.05f) return;
        facingRight = dx > 0f;
        if (sprite != null) sprite.flipX = spriteFacesRight ? !facingRight : facingRight;
    }

    // ------------------------------------------------------------------ damage

    // A chandelier that is falling over the boss hits it
    private void CheckHazards()
    {
        foreach (FallingObject f in hazards)
        {
            if (f == null || f.State != FallingObject.DropState.Falling) continue;
            Collider2D c = f.GetComponent<Collider2D>();
            if (c != null && Overlap2D(c.bounds, bodyCollider.bounds)) TakeDamage();
        }
    }

    // Bounds of 2D colliders have no depth, so two objects at different z never "intersect" in 3D
    private static bool Overlap2D(Bounds a, Bounds b)
    {
        return a.min.x < b.max.x && a.max.x > b.min.x && a.min.y < b.max.y && a.max.y > b.min.y;
    }

    // The cat's super dash hits the boss
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (Current == State.Dead) return;
        CatStateController cs = collision.gameObject.GetComponent<CatStateController>();
        if (cs == null || cs.MoveState != CatMoveState.SuperDashing) return;

        TakeDamage();
        if (obstacleHit != null) obstacleHit.Raise();       // the cat bounces off
    }

    /// <summary>Takes one heart unless the boss is immune. Returns true if it did.</summary>
    public bool TakeDamage()
    {
        if (Current == State.Dead || invulnerableTimer > 0f) return false;

        Hearts--;
        invulnerableTimer = invulnerableTime;
        onHeartsChanged.Invoke();

        if (Hearts <= 0)
        {
            Die();
        }
        else
        {
            Current = State.Hurt;
            stateTimer = 0f;
            Stop();
            Play(ANIM_HURT);
        }
        return true;
    }

    private void Die()
    {
        Current = State.Dead;
        Stop();
        Play(ANIM_DEAD);
        StartCoroutine(LieDown());
        onDefeated.Invoke();
    }

    // The fallen pose is lower than the standing one: put its underside on the floor
    private IEnumerator LieDown()
    {
        float floorY = bodyCollider.bounds.min.y;
        bodyCollider.enabled = false;
        body.simulated = false;
        yield return null;      // the Animator applies the new sprite
        if (sprite != null)
        {
            Vector3 p = transform.position;
            p.y += floorY - sprite.bounds.min.y;
            transform.position = p;
        }
    }

    // GameRestart
    public void ResetState()
    {
        StopAllCoroutines();
        transform.position = startPosition;
        bodyCollider.enabled = true;
        body.simulated = true;
        body.linearVelocity = Vector2.zero;
        Hearts = maxHearts;
        Current = State.Chase;
        invulnerableTimer = 0f;
        biteCooldownTimer = 0f;
        stateTimer = 0f;
        currentAnim = null;
        Play(ANIM_IDLE);
        onHeartsChanged.Invoke();
    }

    private void Play(string state)
    {
        if (animator == null || state == currentAnim) return;
        animator.Play(state);
        currentAnim = state;
    }
}

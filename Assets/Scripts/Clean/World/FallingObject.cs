using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// An object hanging in the air (e.g. an aircon) that drops when the cat lands on top of it. The drop is done
/// here, because its animations only swap the sprite and never move the transform: the object falls with
/// gravity until it reaches the ground, stops there, and then plays the crash animation.
/// Needs a kinematic Rigidbody2D on the same object, a trigger collider that covers the sprite (used to find
/// the ground) and a solid collider on its top for the cat to land on. GameRestart brings it back.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingObject : MonoBehaviour
{
    public enum DropState { Idle, Falling, Crashed }

    [Header("Animator states")]
    public string idleState = "object-idle";
    public string crashState = "crash";         // the Animator state that plays object-crash

    [Header("Fall")]
    public float gravity = 30f;
    public float maxFallSpeed = 25f;
    public string groundTag = "Ground";
    [Tooltip("How far above the object's top the cat's feet must be for a landing to count as 'on top'.")]
    public float topTolerance = 0.6f;
    [Tooltip("On: the drop starts as soon as the cat touches the trigger from any side (chandelier). Off: only a landing on top counts (aircon).")]
    public bool dropOnAnyTouch = false;

    [Header("Events")]
    public UnityEvent onDropStarted = new UnityEvent();
    public UnityEvent onLanded = new UnityEvent();

    public DropState State { get; private set; } = DropState.Idle;

    private Rigidbody2D body;
    private Animator animator;
    private Collider2D coverage;                // the sprite-sized collider that finds the ground
    private ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    private Vector3 startPosition;      // with z: the object hangs in front of or behind other things
    private float fallSpeed;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        startPosition = transform.position;

        // The collider on this object itself covers the whole sprite
        foreach (Collider2D c in GetComponents<Collider2D>())
        {
            if (coverage == null) coverage = c;
        }

        groundFilter = new ContactFilter2D();
        groundFilter.useTriggers = false;
        groundFilter.useLayerMask = false;

        PlayState(idleState);
    }

    // The cat comes down into the sprite-sized trigger...
    void OnTriggerEnter2D(Collider2D other)
    {
        TryDrop(other);
    }

    // ...or lands on a solid collider of the object, if one is switched on
    void OnCollisionEnter2D(Collision2D collision)
    {
        TryDrop(collision.collider);
    }

    private void TryDrop(Collider2D other)
    {
        if (State != DropState.Idle) return;

        CatMotor cat = other.GetComponentInParent<CatMotor>();
        if (cat == null) return;

        if (dropOnAnyTouch)
        {
            StartDrop();
            return;
        }

        // Only a cat coming down onto the top counts: falling, with its feet around the object's top
        float objectTop = coverage != null ? coverage.bounds.max.y : transform.position.y;
        float catFeet = other.bounds.min.y;
        if (cat.Velocity.y > 0.5f || catFeet < objectTop - topTolerance) return;

        StartDrop();
    }

    public void StartDrop()
    {
        if (State != DropState.Idle) return;
        State = DropState.Falling;
        fallSpeed = 0f;
        onDropStarted.Invoke();
    }

    void FixedUpdate()
    {
        if (State != DropState.Falling) return;

        float dt = Time.fixedDeltaTime;
        fallSpeed = Mathf.Min(fallSpeed + gravity * dt, maxFallSpeed);
        float step = fallSpeed * dt;

        float groundDistance = DistanceToGround(step);
        if (groundDistance >= 0f)
        {
            // Touch down exactly on the ground and stop
            body.position = body.position + Vector2.down * groundDistance;
            State = DropState.Crashed;
            fallSpeed = 0f;
            PlayState(crashState);
            onLanded.Invoke();
        }
        else
        {
            body.MovePosition(body.position + Vector2.down * step);
        }
    }

    // Distance the object can still fall before its bottom meets the ground within 'step', or -1 if the way is clear
    private float DistanceToGround(float step)
    {
        if (coverage == null) return -1f;

        Bounds b = coverage.bounds;
        int count = Physics2D.BoxCast(b.center, b.size, 0f, Vector2.down, groundFilter, hits, step + 0.02f);
        float nearest = -1f;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.attachedRigidbody == body) continue;     // part of this object
            if (!c.CompareTag(groundTag)) continue;

            float d = Mathf.Max(0f, hits[i].distance);
            if (nearest < 0f || d < nearest) nearest = d;
        }
        return nearest;
    }

    // GameRestart response: back up where it hung, intact
    public void ResetState()
    {
        State = DropState.Idle;
        fallSpeed = 0f;
        transform.position = startPosition;
        body.position = startPosition;      // the rigidbody only has x and y
        Physics2D.SyncTransforms();
        PlayState(idleState);
    }

    private void PlayState(string stateName)
    {
        if (animator != null && !string.IsNullOrEmpty(stateName)) animator.Play(stateName, 0, 0f);
    }
}

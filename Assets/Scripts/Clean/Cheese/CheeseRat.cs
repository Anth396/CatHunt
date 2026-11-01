using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rat that comes out of a rat hole, runs to the cheese, picks it up and runs back into any open hole,
/// keeping away from the cat. It follows a path over the PlatformNavGraph (walks, jumps, drops) and moves
/// with the same kind of custom physics as the other rats. The cheese game owns it and gets the callbacks.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CheeseRat : MonoBehaviour
{
    public enum Phase { SeekCheese, ReturnToHole }

    [Header("Movement")]
    public float acceleration = 30f;
    public float groundFriction = 15f;
    public bool spriteFacesRight = true;
    public RatNavSettings nav = new RatNavSettings();

    [Header("Cheese")]
    [Tooltip("Shown while the rat carries the cheese.")]
    public GameObject carriedCheese;
    [Tooltip("The rat picks up the cheese when its body centre is this close (x, y).")]
    public Vector2 grabRange = new Vector2(0.9f, 1.8f);
    public Vector2 holeRange = new Vector2(0.7f, 1.2f);

    [Header("Avoiding the cat")]
    [Tooltip("Within this distance the cat makes nearby nodes more expensive.")]
    public float softRadius = 7f;
    [Tooltip("Nodes this close to the cat are never used.")]
    public float hardRadius = 2.5f;
    [Tooltip("Seconds of extra cost for a node right next to the cat.")]
    public float dangerCost = 12f;
    [Tooltip("How much a height difference counts as distance (a cat on another floor is less of a threat).")]
    public float heightWeight = 2f;
    public float replanInterval = 0.2f;
    public float retreatTravelTime = 3f;

    [Header("Safety")]
    public float stuckTime = 2.5f;
    public float maxLifetime = 60f;
    [Tooltip("The rat leaves if there is no way to its goal for this long.")]
    public float noPathTimeout = 6f;

    [Header("Debug")]
    public string debugInfo;
    public string debugJump;
    private string plannedJump;
    private PlatformNavGraph.Edge activeJump;

    public bool IsGrounded => grounded;
    public Phase CurrentPhase { get; private set; } = Phase.SeekCheese;
    public bool Carrying => CurrentPhase == Phase.ReturnToHole;

    // What the cheese game gives the rat
    public Func<Vector2> CheesePosition;                    // where the cheese is
    public Func<IList<RatHole>> OpenHoles;                  // holes the rat may return to
    public Func<RatHole, bool> CanEnterHole;                // false if the hole got covered
    public Func<bool> CatIsHidden;                          // the cat is invisible
    public Transform cat;
    public Action<CheeseRat> OnGrabbedCheese;
    public Action<CheeseRat, RatHole> OnReachedHole;
    public Action<CheeseRat> OnGaveUp;

    private PlatformNavGraph graph;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Animator animator;
    private BoxCollider2D bodyCollider;
    private string currentAnim;

    private readonly HashSet<Collider2D> groundContacts = new HashSet<Collider2D>();
    private readonly HashSet<Collider2D> passableWalls = new HashSet<Collider2D>();
    private Vector2 groundNormal = Vector2.up;
    private bool grounded;
    private float airMomentumX;
    private float moveDir = 1f;
    private float targetSpeed;
    private float lastLeftGround;

    private List<PlatformNavGraph.Edge> path = new List<PlatformNavGraph.Edge>();
    private float replanTimer;
    private float age;
    private float noPathTime;
    private float stuckTimer;
    private Vector2 lastProgressPos;

    const string ANIM_IDLE = "rat-idle", ANIM_RUN = "rat-run", ANIM_JUMP = "rat-jump", ANIM_MID_AIR = "rat-mid-air", ANIM_FALL = "rat-fall";

    // ------------------------------------------------------------------ setup

    /// <summary>Size of a prefab's body collider in the world (for building the nav graph before any rat exists).</summary>
    public static Vector2 BodySizeOf(GameObject prefab)
    {
        BoxCollider2D c = prefab.GetComponent<BoxCollider2D>();
        if (c == null) return new Vector2(1f, 0.7f);
        return Vector2.Scale(c.size, prefab.transform.lossyScale);
    }

    public void Init(PlatformNavGraph navGraph)
    {
        graph = navGraph;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        bodyCollider = GetComponent<BoxCollider2D>();
        if (carriedCheese != null) carriedCheese.SetActive(false);
    }

    void Start()
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // The speed is set by code, so surface friction would only make the rat stick to walls
        bodyCollider.sharedMaterial = new PhysicsMaterial2D("CheeseRat") { friction = 0f, bounciness = 0f };

        // Building walls are for the cat only
        foreach (PassableWhenGrounded wall in FindObjectsByType<PassableWhenGrounded>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (Collider2D c in wall.GetComponents<Collider2D>())
            {
                passableWalls.Add(c);
                Physics2D.IgnoreCollision(bodyCollider, c, true);
            }

        lastProgressPos = body.position;
    }

    /// <summary>Put the rat's feet on a spot.</summary>
    public void PlaceFeetAt(Vector2 feet)
    {
        if (bodyCollider == null) bodyCollider = GetComponent<BoxCollider2D>();
        Vector2 offset = Vector2.Scale(bodyCollider.offset, transform.lossyScale);
        float halfH = bodyCollider.size.y * transform.lossyScale.y * 0.5f;
        Vector3 p = transform.position;
        p.x = feet.x - offset.x;
        p.y = feet.y + halfH - offset.y + 0.02f;
        transform.position = p;
        Physics2D.SyncTransforms();
        if (body == null) body = GetComponent<Rigidbody2D>();
        body.position = p;
    }

    private float FeetY => bodyCollider.bounds.min.y;
    private Vector2 Feet => new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y);

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (graph == null || bodyCollider == null) return;

        float dt = Time.fixedDeltaTime;
        age += dt;
        if (age > maxLifetime)
        {
            OnGaveUp?.Invoke(this);
            enabled = false;
            return;
        }

        Vector2 v = body.linearVelocity;
        CheckGoals();
        if (!enabled) return;

        if (grounded)
        {
            Navigate(ref v);
            if (grounded)
            {
                float along = v.x * moveDir;
                if (targetSpeed <= 0.01f || along < -0.1f) along = Mathf.Lerp(along, 0f, groundFriction * dt);
                else if (along < targetSpeed) along = Mathf.Min(along + acceleration * dt, targetSpeed);
                else along = Mathf.Lerp(along, targetSpeed, groundFriction * dt);
                v.x = along * moveDir;
                v.y = SlopeVelocityY(v.x);
            }
        }
        else
        {
            // Pushed against a wall in the air: let go of the horizontal speed so the rat slides down
            if (Time.time - lastLeftGround > 0.1f && Mathf.Abs(airMomentumX) > 1f && Mathf.Abs(v.x) < 0.3f) airMomentumX = 0f;

            float g = nav.gravity * (v.y < 0f ? nav.fallMultiplier : 1f);
            v.y -= g * dt;
            v.x = airMomentumX;

            // A collision can push the rat; it never goes faster than a jump or a fall allows
            float maxUp = 0f;
            foreach (float j in nav.jumpSpeeds) maxUp = Mathf.Max(maxUp, j);
            v.y = Mathf.Clamp(v.y, -30f, maxUp);
        }

        body.linearVelocity = v;
        CheckStuck(dt);
    }

    private float SlopeVelocityY(float vx)
    {
        if (groundNormal.y > 0.999f || groundNormal.y < 0.2f) return 0f;
        return -vx * groundNormal.x / groundNormal.y;
    }

    // ------------------------------------------------------------------ goals

    private void CheckGoals()
    {
        Vector2 pos = bodyCollider.bounds.center;

        if (CurrentPhase == Phase.SeekCheese)
        {
            if (CheesePosition == null) return;
            Vector2 cheese = CheesePosition();
            if (Mathf.Abs(pos.x - cheese.x) <= grabRange.x && Mathf.Abs(pos.y - cheese.y) <= grabRange.y)
            {
                CurrentPhase = Phase.ReturnToHole;
                if (carriedCheese != null) carriedCheese.SetActive(true);
                path.Clear();
                replanTimer = 0f;
                OnGrabbedCheese?.Invoke(this);
            }
            return;
        }

        IList<RatHole> holes = OpenHoles != null ? OpenHoles() : null;
        if (holes == null) return;
        foreach (RatHole hole in holes)
        {
            Vector2 h = hole.EntryNode != null ? hole.EntryNode.pos + Vector2.up * 0.4f : (Vector2)hole.transform.position;
            if (Mathf.Abs(pos.x - hole.transform.position.x) > holeRange.x || Mathf.Abs(pos.y - h.y) > holeRange.y) continue;
            if (CanEnterHole != null && !CanEnterHole(hole)) continue;
            OnReachedHole?.Invoke(this, hole);
            enabled = false;
            return;
        }
    }

    // ------------------------------------------------------------------ navigation

    private void Navigate(ref Vector2 v)
    {
        replanTimer -= Time.fixedDeltaTime;
        if (replanTimer <= 0f) Replan();

        targetSpeed = 0f;
        if (path.Count == 0) return;

        PlatformNavGraph.Edge e = path[0];
        float x = bodyCollider.bounds.center.x;

        if (e.type == PlatformNavGraph.EdgeType.Walk)
        {
            float dx = e.to.pos.x - x;
            if (e.dir * dx <= 0.08f)        // reached (or passed) the next point
            {
                path.RemoveAt(0);
                return;
            }
            moveDir = e.dir;
            targetSpeed = nav.runSpeed;
            return;
        }

        if (e.IsWalkOff)
        {
            // Run in the direction of the ledge and over it
            moveDir = e.dir;
            targetSpeed = nav.runSpeed;
            return;
        }

        // A jump: line up with its take-off point, then go
        float toStart = e.from.pos.x - x;
        if (Mathf.Abs(toStart) > 0.12f)
        {
            moveDir = Mathf.Sign(toStart);
            targetSpeed = Mathf.Abs(toStart) < 0.6f ? 2.5f : nav.runSpeed;
            return;
        }

        activeJump = e;
        plannedJump = "from " + Feet.ToString("F2") + " node " + e.from.pos.ToString("F1") + " plan " + e.to.pos.ToString("F1") + " vx=" + e.vx + " vy=" + e.vy;
        moveDir = e.dir;
        v = new Vector2(e.dir * e.vx, e.vy);
        airMomentumX = v.x;
        grounded = false;
        groundContacts.Clear();
        lastLeftGround = Time.time;
        path.RemoveAt(0);
        if (path.Count > 0 && path[0].from != e.to) path.Clear();
    }

    private bool CatThreatens(out Vector2 catPos)
    {
        catPos = Vector2.zero;
        if (cat == null) return false;
        if (CatIsHidden != null && CatIsHidden()) return false;
        catPos = cat.position;
        return true;
    }

    private float CatDistance(Vector2 from, Vector2 catPos)
    {
        return Mathf.Sqrt((from.x - catPos.x) * (from.x - catPos.x) + (from.y - catPos.y) * (from.y - catPos.y) * heightWeight * heightWeight);
    }

    private void Replan()
    {
        replanTimer = replanInterval;
        PlatformNavGraph.Node start = graph.Nearest(Feet, 1.0f, 1.5f, 0.8f);
        if (start == null) return;

        bool threat = CatThreatens(out Vector2 catPos);
        Func<PlatformNavGraph.Node, float> extra = null;
        if (threat)
        {
            extra = n =>
            {
                float d = CatDistance(n.pos, catPos);
                if (d < hardRadius && n != start) return float.PositiveInfinity;
                if (d < softRadius) return dangerCost * (softRadius - d) / softRadius;
                return 0f;
            };
        }

        List<PlatformNavGraph.Node> goals = GoalNodes();
        List<PlatformNavGraph.Edge> found = goals.Count > 0 ? graph.FindPath(start, goals, extra) : null;

        if (found == null && threat)
        {
            // No safe way to the goal: get away from the cat for now
            found = graph.FindBestWithin(start, retreatTravelTime, n => CatDistance(n.pos, catPos), extra);
        }
        if (found == null) found = new List<PlatformNavGraph.Edge>();

        path = found;

        // No way to the goal at all (a gate is shut, the cheese cannot be reached): give up after a while
        if (path.Count == 0 && !goals.Contains(start) && !threat) noPathTime += replanInterval;
        else noPathTime = 0f;
        if (noPathTime > noPathTimeout)
        {
            OnGaveUp?.Invoke(this);
            enabled = false;
            return;
        }

        debugInfo = "start=" + start.pos.ToString("F1") + " goals=" + goals.Count + " path=" + path.Count + (path.Count > 0 ? " first=" + path[0].type + "->" + path[0].to.pos.ToString("F1") + " vy=" + path[0].vy : "") + " threat=" + threat;
    }

    private List<PlatformNavGraph.Node> GoalNodes()
    {
        List<PlatformNavGraph.Node> goals = new List<PlatformNavGraph.Node>();
        if (CurrentPhase == Phase.SeekCheese)
        {
            if (CheesePosition != null)
                goals.AddRange(graph.NodesAround(CheesePosition(), grabRange.x * 0.8f, 1.0f, grabRange.y * 0.95f));
        }
        else
        {
            IList<RatHole> holes = OpenHoles != null ? OpenHoles() : null;
            if (holes != null)
                foreach (RatHole hole in holes)
                {
                    PlatformNavGraph.Node n = hole.EntryNode;
                    if (n != null) goals.Add(n);
                }
        }
        return goals;
    }

    private void CheckStuck(float dt)
    {
        if (!grounded || path.Count == 0) { stuckTimer = 0f; lastProgressPos = body.position; return; }
        if (Vector2.Distance(body.position, lastProgressPos) > 0.25f)
        {
            lastProgressPos = body.position;
            stuckTimer = 0f;
            return;
        }
        stuckTimer += dt;
        if (stuckTimer > stuckTime)
        {
            // Not getting anywhere: the move it is on does not work in practice. Plan again, and after two
            // such failures the move is left out of the plans.
            stuckTimer = 0f;
            path[0].failures++;
            path.Clear();
            replanTimer = 0f;
        }
    }

    // ------------------------------------------------------------------ ground contact

    private bool IsGroundTag(Collider2D c)
    {
        return c.CompareTag(nav.groundTag) || c.CompareTag(nav.stairTag);
    }

    private bool TouchGround(Collision2D col)
    {
        if (!IsGroundTag(col.collider)) return false;
        Vector2 best = Vector2.zero;
        for (int i = 0; i < col.contactCount; i++)
        {
            Vector2 n = col.GetContact(i).normal;
            if (n.y > 0.5f && n.y > best.y) best = n;
        }
        if (best.y <= 0f) return false;
        // Still moving away from the surface (the step of a jump): not a landing
        if (Vector2.Dot(body.linearVelocity, best) > 0.1f) return false;

        groundNormal = best;
        groundContacts.Add(col.collider);
        if (!grounded)
        {
            if (activeJump != null)
            {
                // A move that ended somewhere else than planned is not to be trusted; after two misses it is dropped
                PlatformNavGraph.Node landedOn = graph.Nearest(Feet, 0.8f, 1.2f, 0.8f);
                bool missed = landedOn == null;
                if (!missed)
                {
                    float miss = Vector2.Distance(landedOn.pos, activeJump.to.pos);
                    // Another surface than planned (e.g. the stair flight above the target) or far from it
                    missed = miss > 2.0f || (landedOn.support != activeJump.to.support && miss > 0.3f);
                }
                if (missed) activeJump.failures++;
                activeJump = null;
            }
            if (!string.IsNullOrEmpty(plannedJump)) { debugJump = plannedJump + " -> landed " + Feet.ToString("F2") + " on " + col.collider.name; plannedJump = null; }
            grounded = true;
            replanTimer = 0f;
            path.Clear();       // plan again from where it landed
        }
        return true;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!TouchGround(col)) StandingOnCat(col);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (!TouchGround(col)) StandingOnCat(col);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (!groundContacts.Remove(col.collider)) return;
        if (groundContacts.Count > 0 || !grounded) return;

        grounded = false;
        // Walked off an edge: keep the speed. A jump has set its own.
        if (Time.time - lastLeftGround > 0.05f) airMomentumX = body.linearVelocity.x;
        lastLeftGround = Time.time;
        if (path.Count > 0 && path[0].IsWalkOff) { activeJump = path[0]; path.RemoveAt(0); }
        // The rest of the path is unusable once the rat is in the air; it plans again on landing
        if (path.Count > 0 && path[0].type == PlatformNavGraph.EdgeType.Walk) path.Clear();
    }

    // The cat is not ground: a rat that lands on its head hops off to the side
    private void StandingOnCat(Collision2D col)
    {
        if (!col.gameObject.CompareTag("Player")) return;
        Bounds rb = bodyCollider.bounds;
        Bounds cb = col.collider.bounds;
        if (rb.min.y < cb.max.y - 0.2f) return;
        float dx = rb.center.x - cb.center.x;
        airMomentumX = (Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : moveDir) * nav.runSpeed;
    }

    // ------------------------------------------------------------------ animation

    private void UpdateAnimation()
    {
        if (body == null) return;
        Vector2 vel = body.linearVelocity;

        if (sprite != null)
        {
            bool faceLeft = (grounded ? moveDir : Mathf.Sign(airMomentumX == 0f ? moveDir : airMomentumX)) < 0f;
            sprite.flipX = spriteFacesRight ? faceLeft : !faceLeft;
        }
        if (animator == null) return;

        string state;
        if (!grounded)
        {
            if (vel.y > 2f) state = ANIM_JUMP;
            else if (vel.y > -2f) state = ANIM_MID_AIR;
            else state = ANIM_FALL;
        }
        else state = Mathf.Abs(vel.x) > 0.2f ? ANIM_RUN : ANIM_IDLE;

        if (state != currentAnim)
        {
            animator.Play(state);
            currentAnim = state;
        }
    }
}

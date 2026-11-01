using UnityEngine;

/// <summary>
/// A potion. It either stays hidden until revealed (a crate breaking calls Reveal), or sits visible on
/// something (a bookshelf) until Drop() makes it fall to the ground. Touching a collectible potion raises
/// PowerupCollected with its PowerupData; the buff state machine reacts. It knows nothing about the cat.
/// </summary>
public class PotionItem : MonoBehaviour
{
    public PowerupData powerup;
    public PowerupGameEvent powerupCollected;

    [Header("Sitting on a shelf")]
    [Tooltip("Visible from the start but not collectible until it is dropped.")]
    public bool startVisible = false;
    public float gravity = 30f;
    public float maxFallSpeed = 25f;
    public string groundTag = "Ground";
    public string stairTag = "Stair";

    private enum PotionState { Hidden, OnShelf, Falling, Ready }
    private PotionState state = PotionState.Hidden;

    private SpriteRenderer potionSprite;
    private Collider2D potionCollider;
    private Vector3 startLocalPosition;
    private float fallSpeed;
    private ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    void Awake()
    {
        potionSprite = GetComponent<SpriteRenderer>();
        potionCollider = GetComponent<Collider2D>();
        startLocalPosition = transform.localPosition;

        groundFilter = new ContactFilter2D();
        groundFilter.useTriggers = false;
        groundFilter.useLayerMask = false;

        ResetState();
    }

    // A crate breaking: the potion appears where it is and can be collected
    public void Reveal()
    {
        SetVisible(true);
        state = PotionState.Ready;
    }

    // Shelf: the potion lets go and falls to the ground
    public void Drop()
    {
        if (state != PotionState.OnShelf) return;
        SetVisible(true);       // the collider is needed to find the ground and to be collected
        fallSpeed = 0f;
        state = PotionState.Falling;
    }

    public bool IsOnShelf => state == PotionState.OnShelf;

    // Back to the start: hidden, or sitting where it was placed
    public void Hide()
    {
        ResetState();
    }

    // GameRestart response
    public void ResetState()
    {
        transform.localPosition = startLocalPosition;
        fallSpeed = 0f;
        if (startVisible)
        {
            if (potionSprite != null) potionSprite.enabled = true;
            if (potionCollider != null) potionCollider.enabled = false;     // not collectible while it sits there
            state = PotionState.OnShelf;
        }
        else
        {
            SetVisible(false);
            state = PotionState.Hidden;
        }
    }

    void FixedUpdate()
    {
        if (state != PotionState.Falling) return;

        fallSpeed = Mathf.Min(fallSpeed + gravity * Time.fixedDeltaTime, maxFallSpeed);
        float step = fallSpeed * Time.fixedDeltaTime;

        float groundDistance = DistanceToGround(step);
        if (groundDistance >= 0f)
        {
            transform.position += Vector3.down * groundDistance;
            state = PotionState.Ready;
        }
        else
        {
            transform.position += Vector3.down * step;
        }
    }

    // Distance to the ground within 'step', or -1 if the way is clear
    private float DistanceToGround(float step)
    {
        if (potionCollider == null) return -1f;

        Bounds b = potionCollider.bounds;
        int count = Physics2D.BoxCast(b.center, b.size, 0f, Vector2.down, groundFilter, hits, step + 0.02f);
        float nearest = -1f;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c == potionCollider) continue;
            if (!c.CompareTag(groundTag) && !c.CompareTag(stairTag)) continue;

            float d = Mathf.Max(0f, hits[i].distance);
            if (nearest < 0f || d < nearest) nearest = d;
        }
        return nearest;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Only a potion that is out (revealed, or dropped) can be collected
        if (state != PotionState.Ready && state != PotionState.Falling) return;
        if (!other.CompareTag("Player") || other.GetComponentInParent<CatMotor>() == null) return;

        if (powerupCollected != null && powerup != null) powerupCollected.Raise(powerup);
        SetVisible(false);
        state = PotionState.Hidden;
    }

    private void SetVisible(bool visible)
    {
        if (potionSprite != null) potionSprite.enabled = visible;
        if (potionCollider != null) potionCollider.enabled = visible;
    }
}

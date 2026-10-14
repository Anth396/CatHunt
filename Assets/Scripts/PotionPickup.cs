using UnityEngine;

/// <summary>
/// Invisibility potion. Hidden at the start; a BreakableObject reveals it when it breaks.
/// The cat picks it up by touching it, which makes the rats unable to detect the cat for a while.
/// Needs a trigger collider (not an EdgeCollider2D, BreakableObject switches those off).
/// </summary>
public class PotionPickup : MonoBehaviour
{
    public float invisibilityDuration = 8f;     // seconds
    public AudioSource pickupAudio;

    private SpriteRenderer potionSprite;
    private Collider2D potionCollider;

    void Awake()
    {
        potionSprite = GetComponent<SpriteRenderer>();
        potionCollider = GetComponent<Collider2D>();
        SetVisible(false);
    }

    // Called by BreakableObject when it breaks
    public void Reveal()
    {
        SetVisible(true);
    }

    // Called by BreakableObject.ResetState on restart: hidden again until the next break
    public void ResetState()
    {
        SetVisible(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only a revealed potion has an enabled collider, so this never fires while hidden
        if (!other.CompareTag("Player")) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        player.GrantInvisibility(invisibilityDuration);
        if (pickupAudio != null && pickupAudio.clip != null)
        {
            AudioSource.PlayClipAtPoint(pickupAudio.clip, transform.position);
        }
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (potionSprite != null) potionSprite.enabled = visible;
        if (potionCollider != null) potionCollider.enabled = visible;
    }
}

using UnityEngine;

/// <summary>
/// A potion that stays hidden until revealed (a crate breaking calls Reveal). Touching it raises
/// PowerupCollected with its PowerupData; the buff state machine reacts. It knows nothing about the cat.
/// </summary>
public class PotionItem : MonoBehaviour
{
    public PowerupData powerup;
    public PowerupGameEvent powerupCollected;

    private SpriteRenderer potionSprite;
    private Collider2D potionCollider;

    void Awake()
    {
        potionSprite = GetComponent<SpriteRenderer>();
        potionCollider = GetComponent<Collider2D>();
        SetVisible(false);
    }

    public void Reveal()
    {
        SetVisible(true);
    }

    // GameRestart response (also used when the crate is restored)
    public void Hide()
    {
        SetVisible(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Only a revealed potion has an enabled collider
        if (!other.CompareTag("Player") || other.GetComponentInParent<CatMotor>() == null) return;

        if (powerupCollected != null && powerup != null) powerupCollected.Raise(powerup);
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (potionSprite != null) potionSprite.enabled = visible;
        if (potionCollider != null) potionCollider.enabled = visible;
    }
}

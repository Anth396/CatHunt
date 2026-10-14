using System.Collections.Generic;
using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    private bool isBroken = false;
    public AudioSource breakableAudio;
    public int scoreValue = 50;     // points added when this breaks
    public PotionPickup potion;     // optional: revealed when this breaks

    // Everything switched off when the object breaks, so ResetState can switch it back on
    private readonly List<Collider2D> brokenColliders = new List<Collider2D>();
    private readonly List<SpriteRenderer> brokenSprites = new List<SpriteRenderer>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckBreak(other.gameObject);
    }

    // The BoxCollider2D is no longer a trigger, so hits now arrive as collisions
    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckBreak(collision.gameObject);
    }

    private void CheckBreak(GameObject hitObject)
    {
        if (isBroken) return;
        // Check if the object hitting it is the Player and if the player is currently Dashing
        if (hitObject.CompareTag("Player"))
        {
            PlayerMovement player = hitObject.GetComponent<PlayerMovement>();
            if (player != null && player.IsDashing)
            {
                BreakApart();
            }
        }
    }

    private void BreakApart()
    {
        isBroken = true;
        Debug.Log("Breakable object broken by dash!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreValue);
        }
        if (breakableAudio != null && breakableAudio.clip != null)
        {
            breakableAudio.PlayOneShot(breakableAudio.clip);
        }

        if (potion != null) potion.Reveal();

        // 1. This object's own sprite and BoxCollider2D
        DisableSprite(GetComponent<SpriteRenderer>());
        DisableCollider(GetComponent<BoxCollider2D>());

        // 2. The EdgeCollider2D on this object's children (the physical hit box)
        foreach (EdgeCollider2D edge in GetComponentsInChildren<EdgeCollider2D>(true))
        {
            if (potion != null && edge.transform.IsChildOf(potion.transform)) continue;
            DisableCollider(edge);
        }
    }

    // Switched off instead of destroyed so the object can be restored on restart
    private void DisableCollider(Collider2D col)
    {
        if (col == null || !col.enabled) return;
        col.enabled = false;
        brokenColliders.Add(col);
    }

    private void DisableSprite(SpriteRenderer sprite)
    {
        if (sprite == null || !sprite.enabled) return;
        sprite.enabled = false;
        brokenSprites.Add(sprite);
    }

    // Called by GameManager on restart
    public void ResetState()
    {
        foreach (Collider2D col in brokenColliders)
        {
            if (col != null) col.enabled = true;
        }
        foreach (SpriteRenderer sprite in brokenSprites)
        {
            if (sprite != null) sprite.enabled = true;
        }
        if (potion != null) potion.ResetState();
        brokenColliders.Clear();
        brokenSprites.Clear();
        isBroken = false;
    }
}

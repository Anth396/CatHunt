using System.Collections.Generic;
using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    private bool isBroken = false;
    public AudioSource breakableAudio;
    public int scoreValue = 50;     // points added when this breaks

    // Everything switched off when the object breaks, so ResetState can switch it back on
    private readonly List<Collider2D> brokenColliders = new List<Collider2D>();
    private readonly List<SpriteRenderer> brokenSprites = new List<SpriteRenderer>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckBreak(other.gameObject);
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

        // 1. This object's own BoxCollider2D
        DisableCollider(GetComponent<BoxCollider2D>());

        // 2. The parent's BoxCollider2D + sprite, and the parent's other child colliders (physics hit box)
        if (transform.parent != null)
        {
            BoxCollider2D parentCollider = transform.parent.GetComponent<BoxCollider2D>();
            SpriteRenderer parentSprite = transform.parent.GetComponent<SpriteRenderer>();
            if (parentCollider != null)
            {
                DisableCollider(parentCollider);
                DisableSprite(parentSprite);
            }

            BoxCollider2D[] childColliders = transform.parent.GetComponentsInChildren<BoxCollider2D>();
            foreach (BoxCollider2D childCol in childColliders)
            {
                if (childCol.gameObject != gameObject)
                {
                    DisableCollider(childCol);
                }
            }
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
        brokenColliders.Clear();
        brokenSprites.Clear();
        isBroken = false;
    }
}

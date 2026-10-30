using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A crate the dashing cat can smash. Breaking hides its sprite and switches off its colliders (its own box
/// and the edge colliders of its children), raises ObjectBroken with its score value, and fires onBroken
/// (wired in the Inspector, e.g. to reveal a potion). GameRestart brings it back.
/// </summary>
public class BreakableCrate : MonoBehaviour
{
    public int scoreValue = 50;
    public IntGameEvent objectBroken;
    public UnityEvent onBroken = new UnityEvent();
    public UnityEvent onRestored = new UnityEvent();
    public AudioSource breakAudio;

    private bool isBroken;
    private readonly List<Collider2D> disabledColliders = new List<Collider2D>();
    private readonly List<SpriteRenderer> disabledSprites = new List<SpriteRenderer>();

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryBreak(other.gameObject);
    }

    private void TryBreak(GameObject hit)
    {
        if (isBroken || !hit.CompareTag("Player")) return;

        CatStateController cat = hit.GetComponent<CatStateController>();
        if (cat == null || !cat.IsDashing) return;

        isBroken = true;
        if (breakAudio != null && breakAudio.clip != null) breakAudio.PlayOneShot(breakAudio.clip);

        DisableSprite(GetComponent<SpriteRenderer>());
        DisableCollider(GetComponent<BoxCollider2D>());
        foreach (EdgeCollider2D edge in GetComponentsInChildren<EdgeCollider2D>(true))
        {
            DisableCollider(edge);
        }

        if (objectBroken != null) objectBroken.Raise(scoreValue);
        onBroken.Invoke();
    }

    private void DisableCollider(Collider2D col)
    {
        if (col == null || !col.enabled) return;
        col.enabled = false;
        disabledColliders.Add(col);
    }

    private void DisableSprite(SpriteRenderer sprite)
    {
        if (sprite == null || !sprite.enabled) return;
        sprite.enabled = false;
        disabledSprites.Add(sprite);
    }

    // GameRestart response
    public void ResetState()
    {
        foreach (Collider2D col in disabledColliders) if (col != null) col.enabled = true;
        foreach (SpriteRenderer sprite in disabledSprites) if (sprite != null) sprite.enabled = true;
        disabledColliders.Clear();
        disabledSprites.Clear();
        isBroken = false;
        onRestored.Invoke();
    }
}

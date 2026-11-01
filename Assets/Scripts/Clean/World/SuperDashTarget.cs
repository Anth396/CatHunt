using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// An Interactable object (armor, bookshelf...) that only a super dash can hit. The cat passes through it
/// otherwise (CatMotor ignores its collider). On the first super dash hit the cat is knocked back, the object
/// reacts (animation, onHit), and the object stops being interactable until GameRestart.
/// </summary>
public class SuperDashTarget : MonoBehaviour
{
    [Header("Hit")]
    [Tooltip("Raised when the super dash hits; the cat's state machine answers with the knock back and its sound.")]
    public SimpleGameEvent obstacleHit;
    public UnityEvent onHit = new UnityEvent();
    public UnityEvent onRestored = new UnityEvent();

    [Header("Animation (optional)")]
    public Animator animator;
    public string idleState = "";
    public string hitState = "";

    [Tooltip("Super dash hits needed before the object is used up. Every hit knocks the cat back and raises onHit.")]
    [Min(1)] public int hitsRequired = 1;

    private bool used;

    /// <summary>True once the object has taken its last hit (it has fallen / is used up).</summary>
    public bool IsUsed => used;
    private int hits;
    private Collider2D[] solidColliders;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

        // Only the solid colliders on this object: the ones that stop a super dash
        System.Collections.Generic.List<Collider2D> solids = new System.Collections.Generic.List<Collider2D>();
        foreach (Collider2D c in GetComponents<Collider2D>()) if (!c.isTrigger) solids.Add(c);
        solidColliders = solids.ToArray();

        PlayState(idleState);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (used) return;

        // Collisions only happen during a super dash (otherwise the cat ignores this collider), but check anyway
        CatStateController cat = collision.gameObject.GetComponent<CatStateController>();
        if (cat == null || cat.MoveState != CatMoveState.SuperDashing) return;

        hits++;
        if (hits >= hitsRequired)
        {
            used = true;
            foreach (Collider2D c in solidColliders) c.enabled = false;     // no longer interactable
            PlayState(hitState);
        }
        if (obstacleHit != null) obstacleHit.Raise();
        onHit.Invoke();
    }

    // GameRestart response
    public void ResetState()
    {
        used = false;
        hits = 0;
        foreach (Collider2D c in solidColliders) c.enabled = true;
        PlayState(idleState);
        onRestored.Invoke();
    }

    private void PlayState(string stateName)
    {
        if (animator != null && !string.IsNullOrEmpty(stateName)) animator.Play(stateName, 0, 0f);
    }
}

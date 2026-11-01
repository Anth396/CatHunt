using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A decoration with a trigger collider (stand lamp) that falls over when the cat touches it while dashing,
/// super dashing or slashing. Touching it in any other state does nothing. Plays its fall animation once and
/// raises onFall (wire the AudioSource.Play there). GameRestart puts it back.
/// </summary>
public class CatContactFall : MonoBehaviour
{
    [Header("Animation")]
    public Animator animator;
    public string idleState = "";
    public string fallState = "";

    [Header("Events")]
    public UnityEvent onFall = new UnityEvent();
    public UnityEvent onRestored = new UnityEvent();

    public bool HasFallen { get; private set; }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        PlayState(idleState);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Check(other);
    }

    // The cat may already be inside when it starts a slash or dash
    void OnTriggerStay2D(Collider2D other)
    {
        Check(other);
    }

    private void Check(Collider2D other)
    {
        if (HasFallen) return;

        CatStateController cat = other.GetComponentInParent<CatStateController>();
        if (cat == null) return;
        if (!cat.IsDashing && !cat.IsSlashing) return;      // Dashing, SuperDashing or Slashing

        HasFallen = true;
        PlayState(fallState);
        onFall.Invoke();
    }

    // GameRestart response
    public void ResetState()
    {
        HasFallen = false;
        PlayState(idleState);
        onRestored.Invoke();
    }

    private void PlayState(string stateName)
    {
        if (animator != null && !string.IsNullOrEmpty(stateName)) animator.Play(stateName, 0, 0f);
    }
}

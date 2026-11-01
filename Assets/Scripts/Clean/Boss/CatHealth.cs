using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The cat's hearts in the boss fight. A hit takes one heart, knocks the cat away and makes it immune for a
/// moment (the sprite blinks). Losing the last heart raises onDied.
/// </summary>
public class CatHealth : MonoBehaviour
{
    public int maxHearts = 3;
    public float invulnerableTime = 1.5f;
    public float knockbackSpeedX = 8f;
    public float knockbackSpeedY = 7f;
    public AudioSource hurtAudio;

    public UnityEvent onChanged = new UnityEvent();
    public UnityEvent onDied = new UnityEvent();

    public int Hearts { get; private set; }
    public bool IsDead => Hearts <= 0;
    public bool Invulnerable => invulnerableTimer > 0f;

    private CatMotor motor;
    private SpriteRenderer sprite;
    private float invulnerableTimer;

    void Awake()
    {
        motor = GetComponent<CatMotor>();
        sprite = GetComponent<SpriteRenderer>();
        Hearts = maxHearts;
    }

    void Update()
    {
        if (invulnerableTimer > 0f)
        {
            invulnerableTimer -= Time.deltaTime;
            if (sprite != null) sprite.enabled = invulnerableTimer <= 0f || ((int)(Time.time * 12f) % 2 == 0);
        }
    }

    /// <summary>A hit that comes from 'source' (a world position). Returns true if it took a heart.</summary>
    public bool TakeHit(Vector2 source)
    {
        if (IsDead || Invulnerable) return false;

        Hearts--;
        invulnerableTimer = invulnerableTime;
        if (motor != null) motor.Knockback(transform.position.x - source.x, knockbackSpeedX, knockbackSpeedY);
        if (hurtAudio != null && hurtAudio.clip != null) hurtAudio.PlayOneShot(hurtAudio.clip);

        onChanged.Invoke();
        if (Hearts <= 0) onDied.Invoke();
        return true;
    }

    // GameRestart
    public void ResetState()
    {
        Hearts = maxHearts;
        invulnerableTimer = 0f;
        if (sprite != null) sprite.enabled = true;
        onChanged.Invoke();
    }
}

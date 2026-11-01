using UnityEngine;

/// <summary>
/// A doorway to the next scene (the rat hole that leads down to the cellar). Once opened, the cat touching its
/// trigger asks for the target scene through SceneRequested. While open, the sprite pulses to draw the eye.
/// Needs a trigger Collider2D on the same object.
/// </summary>
public class SceneExit : MonoBehaviour
{
    public StringGameEvent sceneRequested;
    public string targetScene = "cellar";
    public bool openFromStart;

    [Header("Look while open")]
    public SpriteRenderer highlight;
    public Color pulseColor = new Color(1f, 0.85f, 0.3f, 1f);
    public float pulseSpeed = 4f;

    public bool IsOpen { get; private set; }

    private bool used;
    private Color normalColor = Color.white;

    void Awake()
    {
        if (highlight == null) highlight = GetComponent<SpriteRenderer>();
        if (highlight != null) normalColor = highlight.color;
    }

    void Start()
    {
        if (openFromStart) Open(); else Close();
    }

    public void Open()
    {
        IsOpen = true;
    }

    // GameRestart
    public void Close()
    {
        IsOpen = false;
        used = false;
        if (highlight != null) highlight.color = normalColor;
    }

    void Update()
    {
        if (!IsOpen || highlight == null) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
        highlight.color = Color.Lerp(normalColor, pulseColor, t);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Touch(other);
    }

    // The cat may already be standing in the hole when it opens
    void OnTriggerStay2D(Collider2D other)
    {
        Touch(other);
    }

    private void Touch(Collider2D other)
    {
        if (!IsOpen || used) return;
        if (other.GetComponentInParent<CatMotor>() == null) return;

        used = true;
        if (sceneRequested != null) sceneRequested.Raise(targetScene);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsOpen ? Color.green : Color.gray;
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

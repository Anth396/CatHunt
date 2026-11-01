using UnityEngine;

/// <summary>A place where a cheese can appear. The cheese is the sprite on this object; it is hidden until the game shows it.</summary>
public class CheeseSpot : MonoBehaviour
{
    private SpriteRenderer sprite;

    public bool IsShown { get; private set; }
    public Vector2 Position => transform.position;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        Hide();
    }

    public void Show()
    {
        IsShown = true;
        if (sprite != null) sprite.enabled = true;
    }

    public void Hide()
    {
        IsShown = false;
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.enabled = false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.6f, 0.6f, 0f));
    }
}

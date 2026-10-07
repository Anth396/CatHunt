using UnityEngine;
using UnityEngine.Events;

// Raises preyCaught when the slashing cat touches the prey. Knows nothing about the GameManager.
public class CatchThePrey : MonoBehaviour
{
    public Transform enemyLocation;
    public PlayerMovement player;       // found automatically when left empty
    public UnityEvent preyCaught;

    void Start()
    {
        if (player == null) player = GetComponentInParent<PlayerMovement>();
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        CheckCatch(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        CheckCatch(collision);
    }

    private void CheckCatch(Collision2D collision)
    {
        // Only counts while the cat is slashing
        if (player == null || !player.IsSlashing) return;

        Collider2D other = collision.collider;
        // Check if the object we touched is the enemy
        if (other.gameObject.CompareTag("Enemy") || other.transform == enemyLocation)
        {
            preyCaught?.Invoke();
        }
    }
}

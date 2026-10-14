using UnityEngine;
using UnityEngine.Events;

// Raises preyCaught when the slashing cat touches the prey. Knows nothing about the GameManager.
public class CatchThePrey : MonoBehaviour
{
    public Transform enemyLocation;
    public PlayerMovement player;       // found automatically when left empty
    public UnityEvent preyCaught;
    public AudioSource caughtAudio;     // played when the rat is caught

    // OnCollisionStay re-checks every physics step; this keeps the sound to once per catch
    private bool caughtThisRound = false;

    void OnCollisionExit2D(Collision2D collision)
    {
        caughtThisRound = false;
    }

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
        if (player == null || !player.IsSlashing)
        {
            caughtThisRound = false;    // ready for the next catch
            return;
        }

        Collider2D other = collision.collider;
        // Check if the object we touched is the enemy
        if (other.gameObject.CompareTag("Enemy") || other.transform == enemyLocation)
        {
            // The game manager ignores repeat catches during a reset, so the sound should too
            if (!caughtThisRound && caughtAudio != null && caughtAudio.clip != null)
            {
                caughtAudio.PlayOneShot(caughtAudio.clip);
            }
            caughtThisRound = true;
            preyCaught?.Invoke();
        }
    }
}

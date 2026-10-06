using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    private bool isBroken = false;
    public AudioSource breakableAudio;

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
        breakableAudio.PlayOneShot(breakableAudio.clip);

        // 1. Destroy BoxCollider2D on this object itself (if it has one)
        BoxCollider2D selfCollider = GetComponent<BoxCollider2D>();
        if (selfCollider != null)
        {
            Destroy(selfCollider);
        }

        // 2. Destroy BoxCollider2D on its parent (if a parent exists and has one) and the child of the parent(physics hit box)
        if (transform.parent != null)
        {
            BoxCollider2D parentCollider = transform.parent.GetComponent<BoxCollider2D>();
            SpriteRenderer parentSprite = transform.parent.GetComponent<SpriteRenderer>();
            if (parentCollider != null)
            {
                Destroy(parentCollider);
                Destroy(parentSprite);
            }
            BoxCollider2D[] childColliders = transform.parent.GetComponentsInChildren<BoxCollider2D>();
            foreach (BoxCollider2D childCol in childColliders)
            {
                // Ensure we don't accidentally re-target the main one if it's already caught
                if (childCol.gameObject != gameObject)
                {
                    Destroy(childCol);
                }
            }
        }


        // Optional: If you want to completely destroy the game object after a short delay or right away:
        // Destroy(gameObject);
        // Or if destroying the parent object entirely:
        // if (transform.parent != null) Destroy(transform.parent.gameObject); else Destroy(gameObject);
    }
}
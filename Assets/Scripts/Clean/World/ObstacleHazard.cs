using UnityEngine;

/// <summary>An obstacle that knocks the cat back: raises ObstacleHit, which the cat's state machine listens to.</summary>
public class ObstacleHazard : MonoBehaviour
{
    public SimpleGameEvent obstacleHit;
    public AudioSource hitAudio;

    void OnCollisionEnter2D(Collision2D collision)
    {
        Hit(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Hit(other.gameObject);
    }

    private void Hit(GameObject hit)
    {
        if (!hit.CompareTag("Player") || hit.GetComponent<CatMotor>() == null) return;

        if (hitAudio != null && hitAudio.clip != null) hitAudio.PlayOneShot(hitAudio.clip);
        if (obstacleHit != null) obstacleHit.Raise();
    }
}

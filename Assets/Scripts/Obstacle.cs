using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public AudioSource obstacleAudio;
    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (!hitObject.CompareTag("Player")) return;

        if (obstacleAudio != null && obstacleAudio.clip != null)
        {
            obstacleAudio.PlayOneShot(obstacleAudio.clip);
        }

        PlayerMovement player = hitObject.GetComponent<PlayerMovement>();
        if (player != null)
        {
            player.Bounce();
        }
    }
}
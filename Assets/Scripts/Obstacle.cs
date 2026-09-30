using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public AudioSource obstacleAudio;
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player")){
            obstacleAudio.PlayOneShot(obstacleAudio.clip);
        }
    }
}
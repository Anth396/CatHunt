using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatchThePrey : MonoBehaviour
{
    public Transform enemyLocation;

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
        if (!GameManager.Instance.IsPlayerSlashing) return;

        Collider2D other = collision.collider;
        // Check if the object we touched is the enemy
        if (other.gameObject.CompareTag("Enemy") || other.transform == enemyLocation)
        {
            Debug.Log("Enemy caught! Timer stopped at: " + GameManager.Instance.timer);
            
            // Tell GameManager to handle the win state/pop-up
            GameManager.Instance.StopGameAndShowWin(GameManager.Instance.timer);
        }
    }
}
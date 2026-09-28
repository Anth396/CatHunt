using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatchThePrey : MonoBehaviour
{
    public Transform enemyLocation;

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object we touched is the enemy
        if (other.gameObject.CompareTag("Enemy") || other.transform == enemyLocation)
        {
            Debug.Log("Enemy caught! Timer stopped at: " + GameManager.Instance.timer);
            
            // Tell GameManager to handle the win state/pop-up
            GameManager.Instance.StopGameAndShowWin(GameManager.Instance.timer);
        }
    }
}
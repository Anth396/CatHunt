using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{

    private float originalX;
    private float maxOffset = 50.0f;
    private float enemyPatroltime = 2.0f;
    private int moveRight = 1;
    private Vector2 velocity;

    private Rigidbody2D enemyBody;
    public Vector3 startPosition = new Vector3(0.0f, 0.0f, 0.0f);

    void Start()
    {
        enemyBody = GetComponent<Rigidbody2D>();
        // get the starting position
        originalX = transform.position.x;
        ComputeVelocity();
    }
    void ComputeVelocity()
    {
        velocity = new Vector2((moveRight) * maxOffset / enemyPatroltime, 0);
    }
    void MoveEnemy()
    {
        enemyBody.MovePosition(enemyBody.position + velocity * Time.fixedDeltaTime);
    }

    // note that this is Update(), which still works but not ideal. See below.
    void Update()
    {
        MoveEnemy();
    }
}
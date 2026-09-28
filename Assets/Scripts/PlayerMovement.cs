using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 

public class PlayerMovement : MonoBehaviour
{
    public float speed = 10;
    public float maxSpeed = 20;
    public float upSpeed = 10;
    private bool onGroundState = true;
    private bool jumpRequest = false;
    private SpriteRenderer catSprite;
    private bool faceRightState = true;
    private Rigidbody2D catBody;

    // Start is called before the first frame update
    void Start()
    {
        catBody = GetComponent<Rigidbody2D>();
        catSprite = GetComponent<SpriteRenderer>();
        catBody.constraints = RigidbodyConstraints2D.FreezeRotation; 
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState)
        {
            jumpRequest = true;
        }
    }

    // FixedUpdate is called 50 times a second
    void FixedUpdate()
    {
        if (Keyboard.current == null) return;

        float moveHorizontal = 0f;
        if (Keyboard.current.dKey.isPressed) moveHorizontal = 1f;
        else if (Keyboard.current.aKey.isPressed) moveHorizontal = -1f;
    
        if (Mathf.Abs(moveHorizontal) > 0)
        {
            Vector2 movement = new Vector2(moveHorizontal, 0);
            
            if (catBody.linearVelocity.magnitude < maxSpeed)
                catBody.AddForce(movement * speed);
        }
        
        bool noMovementKeysPressed = Keyboard.current.aKey.wasReleasedThisFrame || Keyboard.current.dKey.wasReleasedThisFrame;
        if (noMovementKeysPressed)
        {
            catBody.linearVelocity = Vector2.zero;
        }

        if (jumpRequest)
        {
            catBody.AddForce(Vector2.up * upSpeed, ForceMode2D.Impulse);
            onGroundState = false;
            jumpRequest = false;
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Ground")) onGroundState = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Collided with prey!");
            GameManager.Instance.StopGameAndShowWin(GameManager.Instance.timer);
        }
    }

    // Link your UI Restart Button to this method directly via GameManager, 
    // or delegate it like this:
    public void RestartButtonCallback(int input)
    {
        GameManager.Instance.RestartButtonCallback(input);
    }
}
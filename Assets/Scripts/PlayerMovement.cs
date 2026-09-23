using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 1. Added the new Input System namespace
using TMPro;


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
    public TextMeshProUGUI timerText;
    public GameObject enemies;
    public CatchThePrey catchThePrey;
    // Start is called before the first frame update
    void Start()
    {
        // Set to be 30 FPS
        Application.targetFrameRate = 30;
        catBody = GetComponent<Rigidbody2D>();
        catSprite = GetComponent<SpriteRenderer>();
        catBody.constraints = RigidbodyConstraints2D.FreezeRotation; // Fixed repetitive GetComponent call
    }

    // Update is called once per frame
    void Update()
    {
        // Check if keyboard is connected
        if (Keyboard.current == null) return;

        // 2. Fixed legacy Input.GetKeyDown checks
        if (Keyboard.current.spaceKey.wasPressedThisFrame && onGroundState)
        {
            jumpRequest = true;
        }
        
    }

    // FixedUpdate is called 50 times a second
    void FixedUpdate()
    {
        if (Keyboard.current == null) return;

        // 3. Fixed legacy Input.GetAxisRaw("Horizontal")
        float moveHorizontal = 0f;
        if (Keyboard.current.dKey.isPressed) moveHorizontal = 1f;
        else if (Keyboard.current.aKey.isPressed) moveHorizontal = -1f;
    
        if (Mathf.Abs(moveHorizontal) > 0)
        {
            Vector2 movement = new Vector2(moveHorizontal, 0);
            
            // Note: In newer Unity versions, 'linearVelocity' is used instead of 'velocity'
            if (catBody.linearVelocity.magnitude < maxSpeed)
                catBody.AddForce(movement * speed);
        }
        
        // 4. Fixed legacy Input.GetKeyUp
        bool noMovementKeysPressed = Keyboard.current.aKey.wasReleasedThisFrame || Keyboard.current.dKey.wasReleasedThisFrame;
        if (noMovementKeysPressed)
        {
            // stop
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
            Time.timeScale = 0.0f;
        }
    }

    public void RestartButtonCallback(int input)
    {
        Debug.Log("Restart!");
        // reset everything
        ResetGame();
        // resume time
        Time.timeScale = 1.0f;
    }

    private void ResetGame()
    {
        // reset position
        catBody.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        // reset sprite direction
        faceRightState = true;
        catSprite.flipX = false;
        // reset timer
        timerText.text = "Timer - 00:00";
        // reset rat
        foreach (Transform eachChild in enemies.transform)
        {
            eachChild.transform.localPosition = eachChild.GetComponent<EnemyMovement>().startPosition;
            Debug.Log("{} location reset", eachChild);
        }
        // reset timer
        catchThePrey.RestartTimer();
    }
}

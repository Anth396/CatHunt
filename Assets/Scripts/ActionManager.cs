using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the CatActions input asset and raises UnityEvents, so listeners can be wired in the
/// Inspector or with AddListener. Put exactly one of these in the scene.
/// </summary>
public class ActionManager : MonoBehaviour
{
    public static ActionManager Instance { get; private set; }

    [Header("Events")]
    public UnityEvent jump;             // jump button pressed
    public UnityEvent jumpHold;         // jump button held for jumpHoldTime
    public UnityEvent<int> moveCheck;   // -1 left, 0 stopped, 1 right (fires when it changes)
    public UnityEvent dashPressed;      // dash button pressed
    public UnityEvent dashReleased;     // dash button released
    public UnityEvent slash;            // slash button pressed

    [Header("Settings")]
    public float jumpHoldTime = 0.25f;

    // Latest state, for scripts that prefer polling
    public int Move { get; private set; }
    public bool DashHeld { get; private set; }
    public bool JumpHeld { get; private set; }

    private CatActions catActions;
    private float jumpPressTime;
    private bool jumpHoldFired;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        catActions = new CatActions();
    }

    void OnEnable()
    {
        if (catActions == null) return;

        CatActions.GameplayActions g = catActions.gameplay;
        g.move.performed += OnMoveAction;
        g.move.canceled += OnMoveAction;
        g.jump.started += OnJumpAction;
        g.jump.canceled += OnJumpAction;
        g.dash.started += OnDashAction;
        g.dash.canceled += OnDashAction;
        g.slash.started += OnSlashAction;
        g.Enable();
    }

    void OnDisable()
    {
        if (catActions == null) return;

        CatActions.GameplayActions g = catActions.gameplay;
        g.Disable();
        g.move.performed -= OnMoveAction;
        g.move.canceled -= OnMoveAction;
        g.jump.started -= OnJumpAction;
        g.jump.canceled -= OnJumpAction;
        g.dash.started -= OnDashAction;
        g.dash.canceled -= OnDashAction;
        g.slash.started -= OnSlashAction;

        Move = 0;
        DashHeld = false;
        JumpHeld = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        catActions?.Dispose();
    }

    void Update()
    {
        // jumpHold: fires once per press, when the button has stayed down long enough
        if (JumpHeld && !jumpHoldFired && Time.unscaledTime - jumpPressTime >= jumpHoldTime)
        {
            jumpHoldFired = true;
            jumpHold?.Invoke();
        }
    }

    // move: performed on press / change, canceled on release
    private void OnMoveAction(InputAction.CallbackContext context)
    {
        float raw = context.canceled ? 0f : context.ReadValue<float>();
        int value = raw > 0.1f ? 1 : (raw < -0.1f ? -1 : 0);
        if (value == Move) return;

        Move = value;
        moveCheck?.Invoke(value);
    }

    // jump: started = pressed, canceled = released
    private void OnJumpAction(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            JumpHeld = true;
            jumpHoldFired = false;
            jumpPressTime = Time.unscaledTime;
            jump?.Invoke();
        }
        else if (context.canceled)
        {
            JumpHeld = false;
        }
    }

    // dash: started = pressed, canceled = released
    private void OnDashAction(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            DashHeld = true;
            dashPressed?.Invoke();
        }
        else if (context.canceled)
        {
            DashHeld = false;
            dashReleased?.Invoke();
        }
    }

    private void OnSlashAction(InputAction.CallbackContext context)
    {
        slash?.Invoke();
    }
}

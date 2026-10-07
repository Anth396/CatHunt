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
    public UnityEvent<int> moveCheck;   // -1 left, 0 stopped, 1 right (fires when it changes)
    public UnityEvent dashPressed;      // dash button pressed
    public UnityEvent dashReleased;     // dash button released
    public UnityEvent superDashReady;   // dash held long enough (Hold interaction on "super-dash")
    public UnityEvent slash;            // slash button pressed

    // Latest state, for scripts that prefer polling
    public int Move { get; private set; }
    public bool DashHeld { get; private set; }

    private CatActions catActions;

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
        g.dash.started += OnDashAction;
        g.dash.canceled += OnDashAction;
        g.superdash.performed += OnSuperDashAction;
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
        g.dash.started -= OnDashAction;
        g.dash.canceled -= OnDashAction;
        g.superdash.performed -= OnSuperDashAction;
        g.slash.started -= OnSlashAction;

        Move = 0;
        DashHeld = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        catActions?.Dispose();
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

    // jump: fires when the button is pressed
    private void OnJumpAction(InputAction.CallbackContext context)
    {
        jump?.Invoke();
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

    // super-dash: the Hold interaction fires "performed" once the key has been held long enough
    private void OnSuperDashAction(InputAction.CallbackContext context)
    {
        superDashReady?.Invoke();
    }

    private void OnSlashAction(InputAction.CallbackContext context)
    {
        slash?.Invoke();
    }
}

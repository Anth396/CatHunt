using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the CatActions input asset. PlayerMovement reads Move / DashHeld and subscribes to the events.
/// Put exactly one of these in the scene.
/// </summary>
public class ActionManager : MonoBehaviour
{
    public static ActionManager Instance { get; private set; }

    private CatActions catActions;

    // Latest state
    public float Move { get; private set; }         // -1 (left) .. 1 (right)
    public bool DashHeld { get; private set; }

    // One-shot events
    public event Action JumpPressed;
    public event Action DashPressed;
    public event Action DashReleased;
    public event Action SlashPressed;

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
        g.slash.started -= OnSlashAction;

        Move = 0f;
        DashHeld = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        catActions?.Dispose();
    }

    // move: fires on press/change (performed) and on release (canceled)
    private void OnMoveAction(InputAction.CallbackContext context)
    {
        Move = context.canceled ? 0f : context.ReadValue<float>();
    }

    private void OnJumpAction(InputAction.CallbackContext context)
    {
        JumpPressed?.Invoke();
    }

    private void OnSlashAction(InputAction.CallbackContext context)
    {
        SlashPressed?.Invoke();
    }

    // dash: started = pressed, canceled = released
    private void OnDashAction(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            DashHeld = true;
            DashPressed?.Invoke();
        }
        else if (context.canceled)
        {
            DashHeld = false;
            DashReleased?.Invoke();
        }
    }
}

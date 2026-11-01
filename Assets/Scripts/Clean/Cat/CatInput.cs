using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the CatActions input asset and raises ScriptableObject events. It knows nothing about the cat:
/// whatever has a matching GameEventListener hears the input. One per scene, not a singleton.
/// </summary>
public class CatInput : MonoBehaviour
{
    [Header("Events raised")]
    public IntGameEvent moveChanged;            // -1 left, 0 stopped, 1 right (only when it changes)
    public SimpleGameEvent jumpPressed;
    public SimpleGameEvent dashPressed;
    public SimpleGameEvent dashReleased;
    public SimpleGameEvent superDashReady;      // dash held long enough (Hold interaction on "superdash")
    public SimpleGameEvent slashPressed;
    public SimpleGameEvent downPressed;         // drop through a stair
    public SimpleGameEvent downReleased;

    private CatActions actions;
    private int move;

    void OnEnable()
    {
        if (actions == null) actions = new CatActions();

        CatActions.GameplayActions g = actions.gameplay;
        g.move.performed += OnMove;
        g.move.canceled += OnMove;
        g.jump.started += OnJump;
        g.dash.started += OnDash;
        g.dash.canceled += OnDash;
        g.superdash.performed += OnSuperDash;
        g.slash.started += OnSlash;
        g.down.started += OnDown;
        g.down.canceled += OnDown;
        g.Enable();
    }

    void OnDisable()
    {
        if (actions == null) return;

        CatActions.GameplayActions g = actions.gameplay;
        g.Disable();
        g.move.performed -= OnMove;
        g.move.canceled -= OnMove;
        g.jump.started -= OnJump;
        g.dash.started -= OnDash;
        g.dash.canceled -= OnDash;
        g.superdash.performed -= OnSuperDash;
        g.slash.started -= OnSlash;
        g.down.started -= OnDown;
        g.down.canceled -= OnDown;
        move = 0;
    }

    void OnDestroy()
    {
        actions?.Dispose();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        float raw = context.canceled ? 0f : context.ReadValue<float>();
        int value = raw > 0.1f ? 1 : (raw < -0.1f ? -1 : 0);
        if (value == move) return;

        move = value;
        if (moveChanged != null) moveChanged.Raise(value);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (jumpPressed != null) jumpPressed.Raise();
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (dashPressed != null) dashPressed.Raise();
        }
        else if (context.canceled)
        {
            if (dashReleased != null) dashReleased.Raise();
        }
    }

    private void OnSuperDash(InputAction.CallbackContext context)
    {
        if (superDashReady != null) superDashReady.Raise();
    }

    private void OnSlash(InputAction.CallbackContext context)
    {
        if (slashPressed != null) slashPressed.Raise();
    }

    private void OnDown(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            if (downPressed != null) downPressed.Raise();
        }
        else if (context.canceled)
        {
            if (downReleased != null) downReleased.Raise();
        }
    }
}

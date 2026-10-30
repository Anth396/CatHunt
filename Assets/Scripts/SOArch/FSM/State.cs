using UnityEngine;

/// <summary>
/// One state of a pluggable state machine: what to do when entering, every frame and when
/// leaving, plus the transitions that decide the next state.
/// </summary>
[CreateAssetMenu(menuName = "PluggableSM/State")]
public class State : ScriptableObject
{
    public Action[] setupActions;               // once, when the state is entered
    public Action[] actions;                    // every frame
    public EventAction[] eventTriggeredActions; // only when the controller asks for them
    public Action[] exitActions;                // once, when the state is left
    public Transition[] transitions;

    // Colour of the gizmo drawn around the object in the Scene view
    public Color sceneGizmoColor = Color.grey;

    public void UpdateState(StateController controller)
    {
        DoActions(controller);
        CheckTransitions(controller);
    }

    protected void DoActions(StateController controller)
    {
        for (int i = 0; i < actions.Length; i++) actions[i].Act(controller);
    }

    public void DoSetupActions(StateController controller)
    {
        for (int i = 0; i < setupActions.Length; i++) setupActions[i].Act(controller);
    }

    public void DoExitActions(StateController controller)
    {
        for (int i = 0; i < exitActions.Length; i++) exitActions[i].Act(controller);
    }

    public void DoEventTriggeredActions(StateController controller, ActionType type = ActionType.Default)
    {
        foreach (EventAction eventAction in eventTriggeredActions)
        {
            if (eventAction.type == type) eventAction.action.Act(controller);
        }
    }

    protected void CheckTransitions(StateController controller)
    {
        controller.transitionStateChanged = false;
        for (int i = 0; i < transitions.Length; i++)
        {
            // An earlier transition already changed the state: stop, the next frame runs the new state
            if (controller.transitionStateChanged) break;

            bool decisionSucceeded = transitions[i].decision.Decide(controller);
            controller.TransitionToState(decisionSucceeded ? transitions[i].trueState : transitions[i].falseState);
        }
    }
}

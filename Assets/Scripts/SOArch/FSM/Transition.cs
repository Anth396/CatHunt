/// <summary>If the decision is true go to trueState, otherwise to falseState (usually the remain state).</summary>
[System.Serializable]
public class Transition
{
    public Decision decision;
    public State trueState;
    public State falseState;
}

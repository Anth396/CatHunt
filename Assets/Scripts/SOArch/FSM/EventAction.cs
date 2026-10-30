/// <summary>Kinds of event-triggered action a state can respond to.</summary>
public enum ActionType
{
    Default = -1,
    Attack = 0
}

/// <summary>An action that runs only when the controller asks for that type.</summary>
[System.Serializable]
public struct EventAction
{
    public Action action;
    public ActionType type;
}

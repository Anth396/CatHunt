using UnityEngine;
using UnityEngine.Events;

public class SimpleGameEventListener : GameEventListener<Void>
{
    public SimpleGameEvent gameEvent;
    [Tooltip("Public methods to call when the event is raised.")]
    public UnityEvent response = new UnityEvent();

    protected override GameEvent<Void> Source => gameEvent;

    public override void OnEventRaised(Void data)
    {
        response.Invoke();
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class PowerupUnityEvent : UnityEvent<IPowerup> { }

public class PowerupGameEventListener : GameEventListener<IPowerup>
{
    public PowerupGameEvent gameEvent;
    [Tooltip("Public methods taking an IPowerup to call when the event is raised.")]
    public PowerupUnityEvent response = new PowerupUnityEvent();

    protected override GameEvent<IPowerup> Source => gameEvent;

    public override void OnEventRaised(IPowerup data)
    {
        response.Invoke(data);
    }
}

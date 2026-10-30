using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class StringUnityEvent : UnityEvent<string> { }

public class StringGameEventListener : GameEventListener<string>
{
    public StringGameEvent gameEvent;
    [Tooltip("Public methods taking a string to call when the event is raised.")]
    public StringUnityEvent response = new StringUnityEvent();

    protected override GameEvent<string> Source => gameEvent;

    public override void OnEventRaised(string data)
    {
        response.Invoke(data);
    }
}

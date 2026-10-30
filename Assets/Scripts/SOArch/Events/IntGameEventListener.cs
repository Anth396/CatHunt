using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class IntUnityEvent : UnityEvent<int> { }

public class IntGameEventListener : GameEventListener<int>
{
    public IntGameEvent gameEvent;
    [Tooltip("Public methods taking an int to call when the event is raised.")]
    public IntUnityEvent response = new IntUnityEvent();

    protected override GameEvent<int> Source => gameEvent;

    public override void OnEventRaised(int data)
    {
        response.Invoke(data);
    }
}

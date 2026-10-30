using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ScriptableObject event channel. Anything can raise it, anything holding a matching
/// GameEventListener component hears it; neither side knows about the other.
/// Unity cannot serialize an open generic, so each payload type has a small concrete subclass.
/// </summary>
public abstract class GameEvent<T> : ScriptableObject
{
    // Rebuilt every play session: listeners register themselves in OnEnable
    private readonly List<GameEventListener<T>> listeners = new List<GameEventListener<T>>();

    public void Raise(T data)
    {
        // Backwards, so a listener may unregister itself (or be destroyed) while handling the event
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i >= listeners.Count) continue;
            GameEventListener<T> listener = listeners[i];
            if (listener != null) listener.OnEventRaised(data);
        }
    }

    public void RegisterListener(GameEventListener<T> listener)
    {
        if (!listeners.Contains(listener)) listeners.Add(listener);
    }

    public void UnregisterListener(GameEventListener<T> listener)
    {
        listeners.Remove(listener);
    }
}

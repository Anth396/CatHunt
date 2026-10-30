using UnityEngine;

/// <summary>
/// Component that listens to one GameEvent asset. Concrete subclasses pin T and expose the
/// event asset field and the Response (a UnityEvent) in the Inspector.
/// </summary>
public abstract class GameEventListener<T> : MonoBehaviour
{
    // The event asset this listener is subscribed to (the subclass owns the serialized field)
    protected abstract GameEvent<T> Source { get; }

    // Called by the event asset; the subclass invokes its Response
    public abstract void OnEventRaised(T data);

    protected virtual void OnEnable()
    {
        if (Source != null) Source.RegisterListener(this);
    }

    protected virtual void OnDisable()
    {
        if (Source != null) Source.UnregisterListener(this);
    }
}

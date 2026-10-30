using UnityEngine;

public abstract class Variable<T> : ScriptableObject
{
#if UNITY_EDITOR
    [Multiline]
    public string DeveloperDescription = "";
#endif
    protected T _value;

    // The value is not serialized, so it only lives as long as this asset stays in memory. Without this
    // flag Unity unloads the asset when a scene change leaves nothing referencing it (e.g. the loading
    // scene), and the value would silently reset to its default.
    protected virtual void OnEnable()
    {
        hideFlags |= HideFlags.DontUnloadUnusedAsset;
    }

    public T Value
    {
        get { return _value; }
        set { SetValue(value); }
    }

    public abstract void SetValue(T value);
}

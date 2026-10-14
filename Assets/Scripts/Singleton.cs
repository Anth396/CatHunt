using UnityEngine;

/// <summary>
/// Generic MonoBehaviour singleton. The first instance wins; later duplicates destroy themselves.
/// Persistent singletons survive scene loads (root-level GameObjects only, a DontDestroyOnLoad
/// requirement). Scene-bound ones (Persistent => false) are replaced by each scene's own copy.
/// Subclasses that define their own Awake / OnDestroy must override and call base first.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    // Capital "I" to match the existing GameManager.Instance / ActionManager.Instance callers
    public static T Instance => _instance;

    // Override with false for objects that hold references to objects in their own scene
    protected virtual bool Persistent => true;

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            if (Persistent) DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}

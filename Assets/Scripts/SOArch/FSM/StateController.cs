using UnityEngine;

/// <summary>
/// Runs a pluggable state machine on its GameObject. Inherit it to add the data that this
/// character's actions and decisions need (see CatStateController, CatBuffController).
/// </summary>
public abstract class StateController : MonoBehaviour
{
    public State startState;
    public State previousState;
    public State currentState;
    public State remainState;       // dummy state meaning "stay where you are"
    public bool transitionStateChanged = false;

    [HideInInspector] public float stateTimeElapsed;
    public bool isActive = true;    // separate from the GameObject being active

    [Tooltip("Plays the sound effects of this state machine's actions. Added automatically if missing.")]
    public AudioSource audioSource;

    public virtual void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        OnSetupState();
    }

    public void PlayClip(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    public virtual void OnSetupState()
    {
        if (currentState != null) currentState.DoSetupActions(this);
    }

    public virtual void OnExitState()
    {
        stateTimeElapsed = 0f;
        if (currentState != null) currentState.DoExitActions(this);
    }

    public virtual void OnDrawGizmos()
    {
        if (currentState != null)
        {
            Gizmos.color = currentState.sceneGizmoColor;
            Gizmos.DrawWireSphere(transform.position, 1.0f);
        }
    }

    // Only for switching states; no gameplay work belongs here
    public void TransitionToState(State nextState)
    {
        if (nextState == null || nextState == remainState) return;

        OnExitState();
        previousState = currentState;
        currentState = nextState;
        transitionStateChanged = true;
        OnSetupState();
    }

    // True once we have been in the current state for the given time. Call it once per frame.
    public bool CheckIfCountDownElapsed(float duration)
    {
        stateTimeElapsed += Time.deltaTime;
        return stateTimeElapsed >= duration;
    }

    public virtual void Update()
    {
        if (!isActive || currentState == null) return;
        currentState.UpdateState(this);
    }
}

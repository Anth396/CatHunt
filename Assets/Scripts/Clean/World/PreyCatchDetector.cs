using UnityEngine;

/// <summary>
/// On the cat: when the slashing cat touches a rat, raises PreyCaught once per catch. The score keeper and the
/// round resetter react to the event; this script knows neither.
/// </summary>
public class PreyCatchDetector : MonoBehaviour
{
    public SimpleGameEvent preyCaught;
    public AudioSource caughtAudio;
    public string enemyTag = "Enemy";

    private CatStateController cat;
    private bool caughtThisContact;

    void Awake()
    {
        cat = GetComponent<CatStateController>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Check(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        Check(collision);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(enemyTag)) caughtThisContact = false;
    }

    // RoundReset / GameRestart response
    public void ResetState()
    {
        caughtThisContact = false;
    }

    private void Check(Collision2D collision)
    {
        if (caughtThisContact || cat == null || !cat.IsSlashing) return;
        if (!collision.gameObject.CompareTag(enemyTag)) return;

        caughtThisContact = true;
        if (caughtAudio != null && caughtAudio.clip != null) caughtAudio.PlayOneShot(caughtAudio.clip);
        if (preyCaught != null) preyCaught.Raise();
    }
}

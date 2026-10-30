using System.Collections;
using UnityEngine;

/// <summary>After a catch, resets the round (cat and rats back to start) once the current physics step is over.</summary>
public class RoundResetter : MonoBehaviour
{
    public SimpleGameEvent roundReset;
    public BoolVariable gameRunning;

    private bool resetting;

    // PreyCaught response
    public void OnPreyCaught()
    {
        if (resetting) return;
        if (gameRunning != null && !gameRunning.Value) return;

        StartCoroutine(ResetNextFrame());
    }

    private IEnumerator ResetNextFrame()
    {
        resetting = true;
        yield return null;
        if (roundReset != null) roundReset.Raise();
        resetting = false;
    }
}

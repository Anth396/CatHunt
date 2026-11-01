using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rat hole in the wall. Rats come out of it and go back into it. It is covered while any of the items in
/// 'coveredBy' is in its fallen state (the chandelier crashed on the floor, the armor knocked over, the lamp
/// fallen over). A covered hole cannot be used. Map the items in the Inspector.
/// </summary>
public class RatHole : MonoBehaviour
{
    [Tooltip("Items that cover this hole once they have fallen: FallingObject (chandelier), SuperDashTarget (armor, bookshelf), CatContactFall (lamp). Drag the object or the component.")]
    public List<Component> coveredBy = new List<Component>();

    [Tooltip("Never covered, even if items are mapped. Keep one hole like this at the lowest point.")]
    public bool alwaysOpen;

    /// <summary>The standing point in front of the hole (set by the cheese game when it builds the nav graph).</summary>
    public PlatformNavGraph.Node EntryNode { get; set; }

    public bool IsCovered
    {
        get
        {
            if (alwaysOpen) return false;
            foreach (Component c in coveredBy)
            {
                if (c == null) continue;
                if (InFallenState(c)) return true;
            }
            return false;
        }
    }

    private static bool InFallenState(Component c)
    {
        // A GameObject dropped into the list: look at the fall scripts on it
        if (c is FallingObject f) return f.State == FallingObject.DropState.Crashed;
        if (c is SuperDashTarget t) return t.IsUsed;
        if (c is CatContactFall l) return l.HasFallen;

        FallingObject fo = c.GetComponent<FallingObject>();
        if (fo != null) return fo.State == FallingObject.DropState.Crashed;
        SuperDashTarget st = c.GetComponent<SuperDashTarget>();
        if (st != null) return st.IsUsed;
        CatContactFall cf = c.GetComponent<CatContactFall>();
        if (cf != null) return cf.HasFallen;
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsCovered ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        if (coveredBy == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        foreach (Component c in coveredBy) if (c != null) Gizmos.DrawLine(transform.position, c.transform.position);
    }
}

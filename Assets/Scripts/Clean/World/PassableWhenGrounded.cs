using UnityEngine;

/// <summary>
/// A clingable building wall: one edge along the outline of a building. It is solid only to a cat that is in
/// the air with the cling key (shift) held; everything else, including the rats, passes through. With
/// 'clingBothSides' the cat can grab it from either side, otherwise only from the outside of the building.
/// CatMotor and RatController find these markers and handle the collision.
/// </summary>
public class PassableWhenGrounded : MonoBehaviour
{
    [Tooltip("Direction pointing away from the building: -1 for its left side, 1 for its right side.")]
    public int outwardX = -1;

    [Tooltip("On: the wall can be held from both sides. Off: only from the outside (outwardX).")]
    public bool clingBothSides = true;

    void Awake()
    {
        // The one-way effector only blocks from the outside; switch it off so the wall is solid from both sides
        if (clingBothSides && TryGetComponent(out PlatformEffector2D effector)) effector.useOneWay = false;
    }

    /// <summary>True if a contact whose normal points along 'normalX' (from the wall to the cat) may be clung to.</summary>
    public bool CanCling(float normalX)
    {
        return clingBothSides || normalX * outwardX > 0f;
    }
}

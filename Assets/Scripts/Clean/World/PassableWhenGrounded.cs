using UnityEngine;

/// <summary>
/// A clingable building wall: one edge along the outline of a building. It is solid only to a cat that is in
/// the air with the cling key (shift) held, and only from the outside of the building; everything else,
/// including the rats, passes through. CatMotor and RatController find these markers and handle the collision.
/// </summary>
public class PassableWhenGrounded : MonoBehaviour
{
    [Tooltip("Direction pointing away from the building: -1 for its left side, 1 for its right side.")]
    public int outwardX = -1;
}

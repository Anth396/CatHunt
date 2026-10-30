using UnityEngine;

/// <summary>Turns the haste buff on or off: a faster cat, tinted so the buff is visible.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/SetHaste")]
public class SetHasteAction : Action
{
    public bool haste = true;
    public Color hasteTint = new Color(1f, 0.85f, 0.4f, 1f);

    public override void Act(StateController controller)
    {
        CatBuffController buff = (CatBuffController)controller;
        buff.motor.SpeedMultiplier = haste && buff.Constants != null ? buff.Constants.hasteSpeedMultiplier : 1f;
        buff.SetSpriteColor(haste ? hasteTint : Color.white);
    }
}

using UnityEngine;

/// <summary>Turns the cat's invisibility on or off: the shared BoolVariable rats read, and the sprite fade.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/SetInvisible")]
public class SetInvisibleAction : Action
{
    public bool invisible = true;

    public override void Act(StateController controller)
    {
        CatBuffController buff = (CatBuffController)controller;
        if (buff.catInvisible != null) buff.catInvisible.SetValue(invisible);

        Color c = Color.white;
        if (invisible && buff.Constants != null) c.a = buff.Constants.invisibleAlpha;
        buff.SetSpriteColor(c);
    }
}

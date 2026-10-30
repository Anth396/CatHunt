using UnityEngine;

/// <summary>Plays one sound effect through the controller's AudioSource.</summary>
[CreateAssetMenu(menuName = "PluggableSM/Actions/PlaySound")]
public class PlaySoundAction : Action
{
    public AudioClip clip;

    public override void Act(StateController controller)
    {
        controller.PlayClip(clip);
    }
}

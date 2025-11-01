using UnityEngine;

namespace LethalCompanyShisha.Enemies.CustomStateMachineBehaviours;

public class ShishaAnimationHandler : MonoBehaviour
{
    [SerializeField] private ShishaClient shishaClient;

    #region Animation Events
    public void OnAnimationEventDropPoop()
    {
        shishaClient.DropPoop();
    }

    public void OnAnimationEventDeathAnimationComplete()
    {
        StartCoroutine(shishaClient.CompleteDeathSequence());
    }

    public void OnAnimationEventFootstep()
    {
        int clipIndex = Random.Range(0, shishaClient.footstepSfx.Length);
        AudioClip audioClipToPlay = shishaClient.footstepSfx[clipIndex];

        float oldPitch = shishaClient.creatureSfx.pitch;

        //if (shishaClient.creatureSfx.isPlaying) shishaClient.creatureSfx.Stop();

        shishaClient.creatureSfx.pitch = Random.Range(oldPitch - 0.05f, oldPitch + 0.05f);
        shishaClient.creatureSfx.PlayOneShot(audioClipToPlay);
        shishaClient.creatureSfx.pitch = oldPitch;
    }
    #endregion
}
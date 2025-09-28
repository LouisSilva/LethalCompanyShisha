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
    #endregion
}
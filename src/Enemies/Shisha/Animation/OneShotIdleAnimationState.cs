using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha.Enemies.CustomStateMachineBehaviours;

public class OneShotIdleAnimationState : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!netcodeController.HasValue)
        {
            ShishaPlugin.LogVerbose("[OneShotIdleAnimationState] Netcode Controller is null");
            return;
        }

        if (!NetworkManager.Singleton.IsServer || !netcodeController.Value.IsOwner) return;
        ShishaPlugin.LogVerbose("[OneShotIdleAnimationState] Idle cycle complete.");
        netcodeController.Value.OneShotIdleAnimationCompleteServerRpc();
    }
}
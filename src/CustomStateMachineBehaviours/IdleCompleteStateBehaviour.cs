using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha.CustomStateMachineBehaviours;

public class IdleCompleteStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (NetcodeController == null)
        {
            LogDebug("Netcode Controller is null");
            return;
        }

        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Idle cycle complete");
        NetcodeController.IdleCompleteStateBehaviourCallbackServerRpc(ShishaId);
    }
}
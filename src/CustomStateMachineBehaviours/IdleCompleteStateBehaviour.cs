using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha.CustomStateMachineBehaviours;

public class IdleCompleteStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!netcodeController.HasValue)
        {
            ShishaPlugin.LogVerbose("Netcode Controller is null");
            return;
        }

        if (!NetworkManager.Singleton.IsServer || !netcodeController.Value.IsOwner) return;
        ShishaPlugin.LogVerbose("Idle cycle complete.");
        netcodeController.Value.IdleCompleteStateBehaviourCallbackServerRpc();
    }
}
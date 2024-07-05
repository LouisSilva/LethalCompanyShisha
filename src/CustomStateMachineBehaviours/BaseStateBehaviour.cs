using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyShisha.CustomStateMachineBehaviours;

public class BaseStateMachineBehaviour : StateMachineBehaviour
{
    private ManualLogSource _mls;
    protected string ShishaId;
    
    protected ShishaNetcodeController NetcodeController;

    protected void OnEnable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncShishaIdentifier += HandleSyncShishaIdentifier;
    }

    protected void OnDisable()
    {
        if (NetcodeController == null) return;
        NetcodeController.OnSyncShishaIdentifier -= HandleSyncShishaIdentifier;
    }

    public void Initialize(ShishaNetcodeController receivedNetcodeController)
    {
        NetcodeController = receivedNetcodeController;
        OnEnable();
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
    }

    private void HandleSyncShishaIdentifier(string receivedShishaId)
    {
        ShishaId = receivedShishaId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"Shisha Stationary State Behaviour {ShishaId}");
        
        LogDebug("Successfully synced shisha identifier");
    }
    
    protected void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}
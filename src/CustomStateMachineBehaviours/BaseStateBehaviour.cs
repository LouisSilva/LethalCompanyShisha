using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace LethalCompanyShisha.CustomStateMachineBehaviours;

public class BaseStateMachineBehaviour : StateMachineBehaviour
{
    private ManualLogSource _mls;
    protected string ShishaId;
    
    protected readonly NullableObject<ShishaNetcodeController> NetcodeController = new();

    private bool _networkEventsSubscribed;

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public void Initialize(ShishaNetcodeController receivedNetcodeController)
    {
        NetcodeController.Value = receivedNetcodeController;
        SubscribeToNetworkEvents();
    }

    private void HandleSyncShishaIdentifier(string receivedShishaId)
    {
        ShishaId = receivedShishaId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource(
            $"Shisha Animation State Behaviour {ShishaId}");
        
        LogDebug("Successfully synced shisha identifier.");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed || !NetcodeController.IsNotNull) return;
        NetcodeController.Value.OnSyncShishaIdentifier += HandleSyncShishaIdentifier;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed || !NetcodeController.IsNotNull) return;
        NetcodeController.Value.OnSyncShishaIdentifier -= HandleSyncShishaIdentifier;
        _networkEventsSubscribed = false;
    }
    
    protected void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}
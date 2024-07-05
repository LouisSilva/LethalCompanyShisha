using System;
using Unity.Netcode;

namespace LethalCompanyShisha;

public class ShishaNetcodeController : NetworkBehaviour
{
    public event Action<string> OnSyncShishaIdentifier;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string, int> OnChangeBehaviourStateIndex;
    public event Action<string> OnIdleCompleteStateBehaviourCallback;
    public event Action<string, int> OnDoAnimation;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string, NetworkObjectReference, int, int> OnSpawnShishaPoop;
    public event Action<string, int> OnPlayAmbientSfx;
    public event Action<string> OnEnterDeathState;

    [ClientRpc]
    public void EnterDeathStateClientRpc(string receivedShishaId)
    {
        OnEnterDeathState?.Invoke(receivedShishaId);
    }

    [ClientRpc]
    public void PlayAmbientSfxClientRpc(string receivedShishaId, int clipIndex)
    {
        OnPlayAmbientSfx?.Invoke(receivedShishaId, clipIndex);
    }
    
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string receivedShishaId)
    {
        OnInitializeConfigValues?.Invoke(receivedShishaId);
    }

    [ClientRpc]
    public void SpawnShishaPoopClientRpc(string receivedShishaId, NetworkObjectReference poopNetworkObjectReference,
        int variantIndex, int scrapValue)
    {
        OnSpawnShishaPoop?.Invoke(receivedShishaId, poopNetworkObjectReference, variantIndex, scrapValue);
    }

    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string receivedShishaId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(receivedShishaId, animationId, value);
    }

    [ClientRpc]
    public void DoAnimationClientRpc(string receivedShishaId, int animationId)
    {
        OnDoAnimation?.Invoke(receivedShishaId, animationId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void IdleCompleteStateBehaviourCallbackServerRpc(string receivedShishaId)
    {
        OnIdleCompleteStateBehaviourCallback?.Invoke(receivedShishaId);
    }

    [ClientRpc]
    public void ChangeBehaviourStateIndexClientRpc(string receivedShishaId, int newBehaviourStateIndex)
    {
        OnChangeBehaviourStateIndex?.Invoke(receivedShishaId, newBehaviourStateIndex);
    }

    [ClientRpc]
    public void SyncShishaIdentifierClientRpc(string receivedShishaId)
    {
        OnSyncShishaIdentifier?.Invoke(receivedShishaId);
    }
}
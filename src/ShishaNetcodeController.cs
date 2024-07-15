using System;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha;

public class ShishaNetcodeController : NetworkBehaviour
{
    public event Action<string> OnSyncShishaIdentifier;
    public event Action<string> OnIdleCompleteStateBehaviourCallback;
    public event Action<string, int> OnSetAnimationTrigger;
    public event Action<string, NetworkObjectReference, int, int> OnSpawnShishaPoop;
    public event Action<string, int> OnPlayAmbientSfx;
    
    [HideInInspector] public readonly NetworkVariable<int> CurrentBehaviourStateIndex = new();

    [ClientRpc]
    public void PlayAmbientSfxClientRpc(string receivedShishaId, int clipIndex)
    {
        OnPlayAmbientSfx?.Invoke(receivedShishaId, clipIndex);
    }

    [ClientRpc]
    public void SpawnShishaPoopClientRpc(string receivedShishaId, NetworkObjectReference poopNetworkObjectReference,
        int variantIndex, int scrapValue)
    {
        OnSpawnShishaPoop?.Invoke(receivedShishaId, poopNetworkObjectReference, variantIndex, scrapValue);
    }

    [ClientRpc]
    public void SetAnimationTriggerClientRpc(string receivedShishaId, int animationId)
    {
        OnSetAnimationTrigger?.Invoke(receivedShishaId, animationId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void IdleCompleteStateBehaviourCallbackServerRpc(string receivedShishaId)
    {
        OnIdleCompleteStateBehaviourCallback?.Invoke(receivedShishaId);
    }

    [ClientRpc]
    public void SyncShishaIdentifierClientRpc(string receivedShishaId)
    {
        OnSyncShishaIdentifier?.Invoke(receivedShishaId);
    }
}
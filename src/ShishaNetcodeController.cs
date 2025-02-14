using System;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha;

public class ShishaNetcodeController : NetworkBehaviour
{
    internal event Action<string> OnSyncShishaIdentifier;
    internal event Action<string> OnIdleCompleteStateBehaviourCallback;
    internal event Action<string, int> OnSetAnimationTrigger;
    internal event Action<string, NetworkObjectReference, int> OnSpawnShishaPoop;
    internal event Action<string, int> OnPlayAmbientSfx;

    internal event Action<string, int, bool> OnSetAnimationBool;

    [HideInInspector] internal readonly NetworkVariable<int> CurrentBehaviourStateIndex = new();
    [HideInInspector] internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();

    [ClientRpc]
    internal void PlayAmbientSfxClientRpc(string receivedShishaId, int clipIndex)
    {
        OnPlayAmbientSfx?.Invoke(receivedShishaId, clipIndex);
    }

    [ClientRpc]
    internal void SpawnShishaPoopClientRpc(string receivedShishaId, NetworkObjectReference poopNetworkObjectReference, int scrapValue)
    {
        OnSpawnShishaPoop?.Invoke(receivedShishaId, poopNetworkObjectReference, scrapValue);
    }

    [ClientRpc]
    internal void SetAnimationTriggerClientRpc(string receivedShishaId, int animationId)
    {
        OnSetAnimationTrigger?.Invoke(receivedShishaId, animationId);
    }

    [ClientRpc]
    internal void SetAnimationBoolClientRpc(string receivedShishaId, int animationId, bool value)
    {
        OnSetAnimationBool?.Invoke(receivedShishaId, animationId, value);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void IdleCompleteStateBehaviourCallbackServerRpc(string receivedShishaId)
    {
        OnIdleCompleteStateBehaviourCallback?.Invoke(receivedShishaId);
    }

    [ClientRpc]
    internal void SyncShishaIdentifierClientRpc(string receivedShishaId)
    {
        OnSyncShishaIdentifier?.Invoke(receivedShishaId);
    }
}
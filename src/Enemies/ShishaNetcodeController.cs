using System;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha;

public class ShishaNetcodeController : NetworkBehaviour
{
    internal event Action OnIdleCompleteStateBehaviourCallback;
    internal event Action<int> OnSetAnimationTrigger;
    internal event Action<NetworkObjectReference, int> OnSpawnShishaPoop;
    internal event Action<int> OnPlayAmbientSfx;

    internal event Action<int, bool> OnSetAnimationBool;

    [HideInInspector] internal readonly NetworkVariable<int> CurrentBehaviourStateIndex = new();

    [ClientRpc]
    internal void PlayAmbientSfxClientRpc(int clipIndex)
    {
        OnPlayAmbientSfx?.Invoke(clipIndex);
    }

    [ClientRpc]
    internal void SpawnShishaPoopClientRpc(NetworkObjectReference poopNetworkObjectReference, int scrapValue)
    {
        OnSpawnShishaPoop?.Invoke(poopNetworkObjectReference, scrapValue);
    }

    [ClientRpc]
    internal void SetAnimationTriggerClientRpc(int animationId)
    {
        OnSetAnimationTrigger?.Invoke(animationId);
    }

    [ClientRpc]
    internal void SetAnimationBoolClientRpc(int animationId, bool value)
    {
        OnSetAnimationBool?.Invoke(animationId, value);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void IdleCompleteStateBehaviourCallbackServerRpc()
    {
        OnIdleCompleteStateBehaviourCallback?.Invoke();
    }
}
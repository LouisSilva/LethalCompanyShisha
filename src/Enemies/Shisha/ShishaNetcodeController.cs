using LethalCompanyShisha.Util;
using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace LethalCompanyShisha.Enemies;

public class ShishaNetcodeController : NetworkBehaviour
{
    internal event Action OnOneShotIdleAnimationComplete;
    internal event Action<int> OnSetAnimationTrigger;
    internal event Action<NetworkObjectReference, int> OnSpawnShishaPoop;
    internal event Action<int> OnPlayAmbientSfx;
    internal event Action<int, bool> OnSetAnimationBool;
    internal event Action<ShishaServer.Gender> OnSetGender;
    internal event Action<ShishaSkinType> OnSetSkinType;

    private WeightedPicker<ShishaSkinType> _defaultSkinPicker;

    private void Awake()
    {
        _defaultSkinPicker = new WeightedPicker<ShishaSkinType>(
            new List<(ShishaSkinType skin, float weight)>
            {
                (ShishaSkinType.Default1, 1f),
                (ShishaSkinType.Default2, 1f),
                (ShishaSkinType.Default3, 1f)
            });
    }

    [ServerRpc]
    internal void SetSkinTypeServerRpc(ShishaSkinType skinType)
    {
        if (skinType is ShishaSkinType.Default)
        {
            skinType = _defaultSkinPicker.PickOne();
        }

        SetSkinTypeClientRpc(skinType);
    }

    [ClientRpc]
    internal void SetSkinTypeClientRpc(ShishaSkinType skinType)
    {
        OnSetSkinType?.Invoke(skinType);
    }

    [ClientRpc]
    internal void SetGenderClientRpc(ShishaServer.Gender gender)
    {
        OnSetGender?.Invoke(gender);
    }

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
    internal void OneShotIdleAnimationCompleteServerRpc()
    {
        OnOneShotIdleAnimationComplete?.Invoke();
    }
}
using System;
using BepInEx.Logging;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha;

public class ShishaPoopBehaviour : PhysicsProp
{
    private ManualLogSource _mls;
    private string _poopId;
    
    public MeshFilter meshFilter;
    public Mesh[] poopMeshVariants;
    public Material[] poopMaterialVariants;

    public int variantIndex;

    private bool _loadedVariantFromSave;
    private bool _calledRpc;
    public bool isPartOfShisha;

    private ScanNodeProperties _scanNode;

    public override void Start()
    {
        base.Start();
        grabbable = true;
        grabbableToEnemies = true;
        _scanNode = GetComponentInChildren<ScanNodeProperties>();

        if (IsServer)
        {
            _poopId = Guid.NewGuid().ToString();
            _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Poop {_poopId}");
            Random.InitState(FindObjectOfType<StartOfRound>().randomMapSeed + _poopId.GetHashCode());
        }
        
        if (!_calledRpc && !isPartOfShisha) ApplyRandomVariantServerRpc();
    }

    public override void Update()
    {
        //LogDebug($"{itemProperties.itemName} has scan node value: {_scanNode.scrapValue}, scan node text: {_scanNode.subText} and actual value: {scrapValue}");
        _scanNode.scrapValue = scrapValue;
        _scanNode.subText = $"Value: ${scrapValue}";
        if (isHeld && isPartOfShisha) isPartOfShisha = false;
        if (isPartOfShisha) return;
        base.Update();
    }

    public override void LateUpdate()
    {
        if (isPartOfShisha)
        {
            if (transform.parent != null)
            {
                transform.position = transform.parent.position;
                transform.rotation = transform.parent.rotation;
            }
            
            return;
        }
        
        base.LateUpdate();
    }

    public override void EquipItem()
    {
        base.EquipItem();
        isPartOfShisha = false;
    }

    public override void GrabItem()
    {
        base.GrabItem();
        isPartOfShisha = false;
    }

    private void ApplyVariant(int chosenVariantIndex)
    {
        if (_loadedVariantFromSave) return;

        if (poopMaterialVariants.Length > 0 && poopMeshVariants.Length > 0 && poopMaterialVariants.Length == poopMeshVariants.Length)
        {
            mainObjectRenderer.material = poopMaterialVariants[chosenVariantIndex];
            meshFilter.mesh = poopMeshVariants[chosenVariantIndex];
            LogDebug($"New random variant applied with index: {chosenVariantIndex}");
        }
        else
        {
            LogDebug("No material variants available.");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyRandomVariantServerRpc()
    {
        ApplyVariantClientRpc(GetRandomVariantIndex());
    }

    public static int GetRandomVariantIndex()
    {
        int commonCrystalChance = ShishaConfig.Instance.CommonCrystalChance.Value;
        int uncommonCrystalChance = ShishaConfig.Instance.UncommonCrystalChance.Value;
        int rareCrystalChance = ShishaConfig.Instance.RareCrystalChance.Value;
        
        if (commonCrystalChance + uncommonCrystalChance + rareCrystalChance != 100)
        {
            commonCrystalChance = 65;
            uncommonCrystalChance = 25;
            rareCrystalChance = 10;
        }

        int chosenVariantIndex;
        int roll = Random.Range(1, 101);

        if (roll <= commonCrystalChance) chosenVariantIndex = 0;
        else if (roll <= commonCrystalChance + uncommonCrystalChance) chosenVariantIndex = 1;
        else chosenVariantIndex = 2;

        return chosenVariantIndex;
    }

    [ClientRpc]
    private void ApplyVariantClientRpc(int chosenVariantIndex)
    {
        _calledRpc = true;
        ApplyVariant(chosenVariantIndex);
    }
    
    public override int GetItemDataToSave()
    {
        return variantIndex;
    }

    public override void LoadItemSaveData(int saveData)
    {
        variantIndex = saveData;
        ApplyVariant(variantIndex);
        _loadedVariantFromSave = true;
        SetScrapValue(scrapValue);
        LogDebug($"Variant index {variantIndex} APPLIED FROM SAVE DATA.");
    }

    [ClientRpc]
    public void SyncPoopIdClientRpc(string poopId)
    {
        if (IsServer) return;
        _poopId = poopId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Poop {_poopId}");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}
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

    private bool _loadedVariantFromSave;
    private bool _networkEventsSubscribed;

    private ScanNodeProperties _scanNode;

    private readonly NetworkVariable<int> _variantIndex = new(-1);
    private readonly NetworkVariable<bool> _isPartOfShisha = new(true);
    private readonly NetworkVariable<int> _scrapValue = new(ScrapValuePlaceholder);

    private const int ScrapValuePlaceholder = 69420;
    
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        _scanNode = GetComponentInChildren<ScanNodeProperties>();
        SubscribeToNetworkEvents();

        if (IsServer)
        {
            _poopId = Guid.NewGuid().ToString();
            _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Poop {_poopId}");
            Random.InitState(StartOfRound.Instance.randomMapSeed + _poopId.GetHashCode());
            SyncPoopIdClientRpc(_poopId);

            if (!_loadedVariantFromSave)
            {
                _variantIndex.Value = GetRandomVariantIndex();
                _scrapValue.Value = CalculateScrapValue(_variantIndex.Value);
            }
        }
    }

    public override void Update()
    {
        if (isHeld && _isPartOfShisha.Value)
        {
            if (IsServer) _isPartOfShisha.Value = false;
            else SetIsPartOfShishaServerRpc(false);
        }
        
        if (_isPartOfShisha.Value) return;
        base.Update();
    }

    public override void LateUpdate()
    {
        if (_isPartOfShisha.Value)
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

    [ServerRpc(RequireOwnership = false)]
    private void SetIsPartOfShishaServerRpc(bool value)
    {
        _isPartOfShisha.Value = value;
    }

    public override void EquipItem()
    {
        base.EquipItem();
        if (_isPartOfShisha.Value)
        {
            if (IsServer) _isPartOfShisha.Value = false;
            else SetIsPartOfShishaServerRpc(false);
        }
    }

    public override void GrabItem()
    {
        base.GrabItem();
        if (_isPartOfShisha.Value)
        {
            if (IsServer) _isPartOfShisha.Value = false;
            else SetIsPartOfShishaServerRpc(false);
        }
    }

    private static int CalculateScrapValue(int variant)
    {
        return variant switch
        {
            0 => Random.Range(ShishaConfig.Instance.CommonCrystalMinValue.Value, ShishaConfig.Instance.CommonCrystalMaxValue.Value + 1),
            1 => Random.Range(ShishaConfig.Instance.UncommonCrystalMinValue.Value, ShishaConfig.Instance.UncommonCrystalMaxValue.Value + 1),
            2 => Random.Range(ShishaConfig.Instance.RareCrystalMinValue.Value, ShishaConfig.Instance.RareCrystalMaxValue.Value + 1),
            _ => 1
        };
    }

    private void ApplyVariant(int chosenVariantIndex)
    {
        if (poopMaterialVariants.Length > 0 && poopMeshVariants.Length > 0 && poopMaterialVariants.Length == poopMeshVariants.Length)
        {
            mainObjectRenderer.material = poopMaterialVariants[chosenVariantIndex];
            meshFilter.mesh = poopMeshVariants[chosenVariantIndex];
            LogDebug($"New random variant applied with index: {chosenVariantIndex}");
        }
        else
        {
            _mls.LogError($"No material variants available with index: {chosenVariantIndex}.");
        }
    }

    private static int GetRandomVariantIndex()
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

    private void OnScrapValueChanged(int oldValue, int newValue)
    {
        scrapValue = newValue;
        _scanNode.scrapValue = newValue;
        _scanNode.subText = $"Value: ${newValue}";
    }

    private void OnVariantIndexChanged(int oldValue, int newValue)
    {
        ApplyVariant(newValue);
    }

    private void OnIsPartOfShishaChanged(bool oldValue, bool newValue)
    {
        grabbableToEnemies = !newValue;
        grabbable = !newValue;
        fallTime = !newValue ? 1f : 0f;
    }
    
    public override int GetItemDataToSave()
    {
        return int.Parse($"{_variantIndex.Value + 1}{Mathf.Max(_scrapValue.Value, 0)}");
    }

    public override void LoadItemSaveData(int saveData)
    {
        // This is called by the server only
        string saveDataStr = $"{saveData}";
        _variantIndex.Value = int.Parse($"{saveDataStr[0]}") - 1;
        _scrapValue.Value = int.Parse($"{saveDataStr[1..]}");
        _loadedVariantFromSave = true;
        LogDebug($"Variant index {_variantIndex.Value} APPLIED FROM SAVE DATA.");
    }

    [ClientRpc]
    public void SyncPoopIdClientRpc(string poopId)
    {
        if (IsServer) return;
        _poopId = poopId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Poop {_poopId}");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;

        _variantIndex.OnValueChanged += OnVariantIndexChanged;
        _isPartOfShisha.OnValueChanged += OnIsPartOfShishaChanged;
        _scrapValue.OnValueChanged += OnScrapValueChanged;

        _networkEventsSubscribed = true;
    }
    
    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        
        _variantIndex.OnValueChanged -= OnVariantIndexChanged;
        _isPartOfShisha.OnValueChanged -= OnIsPartOfShishaChanged;
        _scrapValue.OnValueChanged -= OnScrapValueChanged;

        _networkEventsSubscribed = false;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls.LogInfo(msg);
        #endif
    }
}
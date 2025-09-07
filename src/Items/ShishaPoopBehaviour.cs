using HarmonyLib;
using System.Diagnostics.CodeAnalysis;
using Unity.Netcode;

namespace LethalCompanyShisha;

public class ShishaPoopBehaviour : PhysicsProp
{
    private bool _networkEventsSubscribed;

    private readonly NetworkVariable<bool> _isPartOfShisha = new();

    public enum PoopType
    {
        Common,
        Uncommon,
        Rare
    }

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
        SubscribeToNetworkEvents();
    }

    public override void Update()
    {
        if (isHeld) EvaluateIsPartOfShisha();
        if (_isPartOfShisha.Value) return;
        base.Update();
    }

    public override void LateUpdate()
    {
        if (_isPartOfShisha.Value)
        {
            if (transform.parent)
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
        EvaluateIsPartOfShisha();
    }

    public override void GrabItem()
    {
        base.GrabItem();
        EvaluateIsPartOfShisha();
    }

    [ServerRpc(RequireOwnership = false)]
    internal void SetIsPartOfShishaServerRpc(bool value)
    {
        _isPartOfShisha.Value = value;
    }

    private void EvaluateIsPartOfShisha()
    {
        if (!_isPartOfShisha.Value) return;

        if (IsServer) _isPartOfShisha.Value = false;
        else SetIsPartOfShishaServerRpc(false);
    }

    [HarmonyPatch(typeof(BeltBagItem), nameof(BeltBagItem.PutObjectInBagLocalClient))]
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static void TriggerHeldActions(BeltBagItem __instance, GrabbableObject gObject)
    {
        if (gObject is ShishaPoopBehaviour shishaPoop)
            shishaPoop.EquipItem();
    }

    private void OnIsPartOfShishaChanged(bool oldValue, bool newValue)
    {
        grabbableToEnemies = !newValue;
        grabbable = !newValue;
        fallTime = !newValue ? 1f : 0f;
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        _isPartOfShisha.OnValueChanged += OnIsPartOfShishaChanged;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        _isPartOfShisha.OnValueChanged -= OnIsPartOfShishaChanged;
        _networkEventsSubscribed = false;
    }
}
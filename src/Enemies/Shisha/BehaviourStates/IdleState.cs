using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using LethalCompanyShisha.Util;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.Idle)]
internal class IdleState : BehaviourState<ShishaServer.States, ShishaServer>
{
    private int _currentIdleAnimation;
    private double _idleCycleEndTime;
    private bool _isIdleAnimationLooping;

    private readonly WeightedPicker<int> _idleAnimationPicker;

    public IdleState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];

        float poopProbabilityWeight = EnemyAIInstance.Context.Blackboard.IsPoopBehaviourEnabled
            ? EnemyAIInstance.Context.Blackboard.PoopProbability
            : 0;

        _idleAnimationPicker = new WeightedPicker<int>(
            new List<(int item, float weight)>
            {
                (ShishaClient.DoPoop, poopProbabilityWeight),
                (ShishaClient.IsGrazing, 0.7f),
                (ShishaClient.IsLyingDown, 0.25f)
            });
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.KillAllSpeed();
        EnemyAIInstance.Context.Adapter.Agent.isStopped = true;

        _currentIdleAnimation = EnemyAIInstance.Context.Blackboard.IsWanderEnabled
            ? _idleAnimationPicker.PickOne()
            : ShishaClient.IsLyingDown;

        if (_currentIdleAnimation == ShishaClient.DoPoop)
        {
            SpawnShishaPoop();
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(_currentIdleAnimation);
            _isIdleAnimationLooping = false;
        }
        // If more one shot idle animations are added in the future, this if statement will need to be changed to something that can handle both one shot and bool idle animations
        else
        {
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationBoolClientRpc(_currentIdleAnimation, true);
            _isIdleAnimationLooping = true;
            _idleCycleEndTime = NetworkManager.Singleton.ServerTime.Time + Random.Range(10f, 60f);
        }
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.ManageAmbientSfx();
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (EnemyAIInstance.Context.Blackboard.IsWanderEnabled &&
            _isIdleAnimationLooping &&
            NetworkManager.Singleton.ServerTime.Time >= _idleCycleEndTime)
        {
            EnemyAIInstance.SwitchBehaviourState(ShishaServer.States.Roaming);
        }
    }

    internal override void OnStateExit(StateTransition<ShishaServer.States, ShishaServer> transition)
    {
        base.OnStateExit(transition);

        if (_isIdleAnimationLooping)
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationBoolClientRpc(_currentIdleAnimation, false);

        EnemyAIInstance.Context.Adapter.Agent.isStopped = false;
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case "OneShotIdleAnimationComplete":
            {
                if (_isIdleAnimationLooping)
                {
                    EnemyAIInstance.LogVerbose($"The event {eventName} was invoked even though this idle animation is a looping one.");
                    break;
                }

                EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(ShishaClient.ForceWalk);
                EnemyAIInstance.SwitchBehaviourState(ShishaServer.States.Roaming);

                break;
            }
        }
    }

    private void SpawnShishaPoop()
    {
        if (!EnemyAIInstance.IsServer) return;
        if (!EnemyAIInstance.Context.Blackboard.PoopPlaceholder)
        {
            ShishaPlugin.Logger.LogError("The poop placeholder transform is null.");
            return;
        }

        ShishaPoopBehaviour.PoopType variant = EnemyAIInstance.Context.Blackboard.PoopPicker.PickOne();

        GameObject poopObject = Object.Instantiate(
            GetPoopItemFromType(variant).spawnPrefab,
            EnemyAIInstance.Context.Blackboard.PoopPlaceholder.position,
            EnemyAIInstance.Context.Blackboard.PoopPlaceholder.rotation,
            EnemyAIInstance.Context.Blackboard.PoopPlaceholder);

        if (!poopObject.TryGetComponent(out ShishaPoopBehaviour poopBehaviour))
        {
            EnemyAIInstance.LogError("The poop object has no ShishaPoopBehaviour. The prefab is probably broken.");
            Object.Destroy(poopObject);
            return;
        }

        int scrapValue = CalculateScrapValue(variant);
        poopBehaviour.SetScrapValue(scrapValue);

        poopObject.GetComponent<NetworkObject>().Spawn();
        EnemyAIInstance.Context.Blackboard.NetcodeController.SpawnShishaPoopClientRpc(poopObject, scrapValue);
    }

    private static Item GetPoopItemFromType(ShishaPoopBehaviour.PoopType variant)
    {
        return variant switch
        {
            ShishaPoopBehaviour.PoopType.Common => ShishaPlugin.ShishaRedPoopItem,
            ShishaPoopBehaviour.PoopType.Uncommon => ShishaPlugin.ShishaGreenPoopItem,
            ShishaPoopBehaviour.PoopType.Rare => ShishaPlugin.ShishaBluePoopItem,
            _ => ShishaPlugin.ShishaRedPoopItem
        };
    }

    private static int CalculateScrapValue(ShishaPoopBehaviour.PoopType variant)
    {
        return variant switch
        {
            ShishaPoopBehaviour.PoopType.Common => Random.Range(ShishaPlugin.Config.CommonCrystalMinValue,
                ShishaPlugin.Config.CommonCrystalMaxValue + 1),
            ShishaPoopBehaviour.PoopType.Uncommon => Random.Range(ShishaPlugin.Config.UncommonCrystalMinValue,
                ShishaPlugin.Config.UncommonCrystalMaxValue + 1),
            ShishaPoopBehaviour.PoopType.Rare => Random.Range(ShishaPlugin.Config.RareCrystalMinValue,
                ShishaPlugin.Config.RareCrystalMaxValue + 1),
            _ => 1
        };
    }
}
using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.Spawning)]
internal class SpawningState : BehaviourState<ShishaServer.States, ShishaServer>
{
    public SpawningState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.SetMovementProfile(0f, 50f);
        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.ShishaFidelityProfile);

        EnemyAIInstance.Context.Blackboard.SpawnPosition = EnemyAIInstance.Context.Adapter.Transform.position;

        if (!EnemyAIInstance.Context.Blackboard.PartnerShisha)
        {
            EnemyAIInstance.LogVerbose("Partner Shisha is null, spawning a Shisha of the opposite gender...");
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetGenderClientRpc(EnemyAIInstance.Context.Blackboard.Gender);
            SpawnPartnerShisha();
        }

        EnemyAIInstance.SwitchBehaviourState(EnemyAIInstance.Context.Blackboard.IsWanderEnabled ? ShishaServer.States.Roaming : ShishaServer.States.Idle);
    }

    private void SpawnPartnerShisha()
    {
        ShishaServer.Gender genderToSpawn = EnemyAIInstance.Context.Blackboard.Gender == ShishaServer.Gender.Female
            ? ShishaServer.Gender.Male
            : ShishaServer.Gender.Female;

        GameObject spawnedShishaObj = Object.Instantiate(ShishaPlugin.Instance.ShishaEnemyType.enemyPrefab,
            EnemyAIInstance.Context.Adapter.Transform.position, Quaternion.identity);

        if (spawnedShishaObj.TryGetComponent(out ShishaServer spawnedShisha))
        {
            spawnedShisha.Context.Blackboard.PartnerShisha = EnemyAIInstance;
            spawnedShisha.Context.Blackboard.Gender = genderToSpawn;
        }

        NetworkObject spawnedShishaNetObj = spawnedShishaObj.GetComponent<NetworkObject>();
        if (!spawnedShishaNetObj) spawnedShishaNetObj = spawnedShishaObj.GetComponentInChildren<NetworkObject>();
        if (!spawnedShishaNetObj)
        {
            EnemyAIInstance.LogWarning($"Could not find the NetworkObject on {spawnedShishaObj.name}. This Shisha's partner will not be spawned.");
            return;
        }

        spawnedShishaNetObj.Spawn(destroyWithScene: true);
        RoundManager.Instance.currentEnemyPower += ShishaPlugin.Instance.ShishaEnemyType.PowerLevel;
        spawnedShisha.Context.Blackboard.NetcodeController.SetGenderClientRpc(spawnedShisha.Context.Blackboard.Gender);
    }

    // private IEnumerator CompleteSpawnSequence()
    // {
    //     yield return new WaitForSeconds(0.1f);
    //     yield return null;
    //
    //     try
    //     {
    //         if (!EnemyAIInstance.Context.Blackboard.PartnerShisha)
    //         {
    //             EnemyAIInstance.LogVerbose("Partner Shisha is null, spawning a Shisha of the opposite gender...");
    //             EnemyAIInstance.Context.Blackboard.NetcodeController.SetGenderClientRpc(EnemyAIInstance.Context
    //                 .Blackboard.Gender);
    //             SpawnPartnerShisha();
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         EnemyAIInstance.LogError(ex);
    //     }
    //
    //     EnemyAIInstance.SwitchBehaviourState(EnemyAIInstance.Context.Blackboard.IsWanderEnabled ? ShishaServer.States.Roaming : ShishaServer.States.Idle);
    // }
}
using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using LethalCompanyShisha.Enemies.Transitions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.Roaming)]
internal class RoamingState : BehaviourState<ShishaServer.States, ShishaServer>
{
    public RoamingState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToArrivingAtIdlePosition(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.SetMovementProfile(ShishaPlugin.Config.MaxSpeed, ShishaPlugin.Config.Acceleration);
        EnemyAIInstance.Context.Blackboard.WanderCycleEndTime = NetworkManager.Singleton.ServerTime.Time + Random.Range(ShishaPlugin.Config.WanderTimeMin, ShishaPlugin.Config.WanderTimeMax);

        EnemyAIInstance.StartSearch(EnemyAIInstance.Context.Blackboard.IsAnchoredWanderEnabled ?
            EnemyAIInstance.Context.Blackboard.SpawnPosition : EnemyAIInstance.Context.Adapter.Transform.position, EnemyAIInstance.roamSearchRoutine);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.Context.Adapter.MoveAgent();
        EnemyAIInstance.ManageAmbientSfx();
    }

    internal override void OnStateExit(StateTransition<ShishaServer.States, ShishaServer> transition)
    {
        base.OnStateExit(transition);

        if (EnemyAIInstance.roamSearchRoutine.inProgress)
            EnemyAIInstance.StopSearch(EnemyAIInstance.roamSearchRoutine);
    }
}
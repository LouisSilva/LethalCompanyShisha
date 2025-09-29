using LethalCompanyShisha.Core.AI;
using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using LethalCompanyShisha.Enemies.Transitions;
using UnityEngine;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.RunningAway)]
internal class RunningAwayState : BehaviourState<ShishaServer.States, ShishaServer>
{
    public RunningAwayState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionFromRunningAway(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        Vector3 runAwayPosition = BaseAI.GetFarthestValidNodeFromPosition(out BaseAI.PathStatus pathStatus,
            EnemyAIInstance.Context.Adapter.Agent,
            EnemyAIInstance.Context.Adapter.TargetPlayer ?
                EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position : EnemyAIInstance.Context.Adapter.Transform.position,
            EnemyAIInstance.allAINodes
        ).position;

        if (pathStatus == BaseAI.PathStatus.Invalid)
        {
            EnemyAIInstance.LogWarning("Could not find a valid node to escape to.");
            EnemyAIInstance.SwitchBehaviourState(ShishaServer.States.Roaming);
            return;
        }

        EnemyAIInstance.Context.Blackboard.RunAwayPosition = runAwayPosition;
        EnemyAIInstance.Context.Adapter.MoveToDestination(runAwayPosition);
        EnemyAIInstance.Context.Adapter.SetMovementProfile(ShishaPlugin.Config.RunningAwayMaxSpeed, ShishaPlugin.Config.Acceleration);
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

        EnemyAIInstance.Context.Adapter.StopAllPathing();
    }
}
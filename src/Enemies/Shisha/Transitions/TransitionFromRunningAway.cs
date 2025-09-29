using LethalCompanyShisha.Core.AI.StateMachine;
using UnityEngine;
using UnityEngine.AI;

namespace LethalCompanyShisha.Enemies.Transitions;

internal class TransitionFromRunningAway : StateTransition<ShishaServer.States, ShishaServer>
{
    public TransitionFromRunningAway(ShishaServer enemyAIInstance) : base(enemyAIInstance)
    {
    }

    internal override bool ShouldTransitionBeTaken()
    {
        NavMeshAgent agent = EnemyAIInstance.Context.Adapter.Agent;

        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance &&
               (EnemyAIInstance.Context.Adapter.Transform.position - EnemyAIInstance.Context.Blackboard.RunAwayPosition).sqrMagnitude <= 6;
    }

    internal override ShishaServer.States NextState()
    {
        if (!EnemyAIInstance.Context.Blackboard.IsWanderEnabled)
            return ShishaServer.States.ArrivingAtIdlePosition;

        return Random.Range(0, 2) == 0
            ? ShishaServer.States.Roaming
            : ShishaServer.States.ArrivingAtIdlePosition;
    }
}
using LethalCompanyShisha.Core.AI.StateMachine;
using Unity.Netcode;

namespace LethalCompanyShisha.Enemies.Transitions;

internal class TransitionToArrivingAtIdlePosition : StateTransition<ShishaServer.States, ShishaServer>
{
    public TransitionToArrivingAtIdlePosition(ShishaServer enemyAIInstance) : base(enemyAIInstance)
    {
    }

    internal override bool ShouldTransitionBeTaken()
    {
        return NetworkManager.Singleton.ServerTime.Time >= EnemyAIInstance.Context.Blackboard.WanderCycleEndTime;
    }

    internal override ShishaServer.States NextState()
    {
        return ShishaServer.States.ArrivingAtIdlePosition;
    }
}
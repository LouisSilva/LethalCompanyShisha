using LethalCompanyShisha.Core.AI.StateMachine;

namespace LethalCompanyShisha.Enemies.Transitions;

internal class TransitionToIdle : StateTransition<ShishaServer.States, ShishaServer>
{
    public TransitionToIdle(ShishaServer enemyAIInstance) : base(enemyAIInstance)
    {
    }

    internal override bool ShouldTransitionBeTaken()
    {
        return EnemyAIInstance.Context.Adapter.Agent.velocity.sqrMagnitude < 0.01f;
    }

    internal override ShishaServer.States NextState()
    {
        return ShishaServer.States.Idle;
    }
}
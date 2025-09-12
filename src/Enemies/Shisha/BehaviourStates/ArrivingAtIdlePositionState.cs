using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using LethalCompanyShisha.Enemies.Transitions;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.ArrivingAtIdlePosition)]
internal class ArrivingAtIdlePositionState : BehaviourState<ShishaServer.States, ShishaServer>
{
    public ArrivingAtIdlePositionState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToIdle(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.BeginGracefulStop();
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.ManageAmbientSfx();
    }
}
using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.StateMachine;
using UnityEngine.Scripting;

namespace LethalCompanyShisha.Enemies.BehaviourStates;

[Preserve]
[State(ShishaServer.States.Dead)]
internal class DeadState : BehaviourState<ShishaServer.States, ShishaServer>
{
    public DeadState(ShishaServer enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.KillAllSpeed();

        EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationBoolClientRpc(ShishaClient.IsDead, true);

        EnemyAIInstance.KillEnemyServerRpc(false);
    }
}
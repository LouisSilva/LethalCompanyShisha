using System.Collections;
using GameNetcodeStuff;
using LethalCompanyShisha.Core.AI;
using LethalCompanyShisha.Core.AI.StateMachine;
using LethalCompanyShisha.Core.Integration;
using LethalCompanyShisha.Util;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha.Enemies;

public class ShishaServer : StateManagedAI<ShishaServer.States, ShishaServer>
{
#pragma warning disable 0649
    [SerializeField] public AISearchRoutine RoamSearchRoutine;
    [SerializeField] private Transform poopPlaceholder;
    [SerializeField] public ShishaNetcodeController netcodeController;
#pragma warning restore 0649

    public enum States
    {
        Spawning,
        Roaming,
        ArrivingAtIdlePosition,
        Idle,
        RunningAway,
        Dead,
    }

    public enum Gender
    {
        Male,
        Female
    }

    private float _ambientAudioTimer;
    private float _lastHitTime;

    private int _numberOfAmbientAudioClips;

    private bool _networkEventsSubscribed;
    private static bool _hasRegisteredImperiumInsights;

    public AIContext<ShishaBlackboard, ShishaAdapter> Context { get; private set; }
    private ShishaBlackboard _blackboard => Context.Blackboard;
    private ShishaAdapter _adapter => Context.Adapter;

    private void Awake()
    {
        ShishaBlackboard blackboard = new();
        ShishaAdapter adapter = new(this);

        Context = new AIContext<ShishaBlackboard, ShishaAdapter>(blackboard, adapter);

        _blackboard.PoopPicker = new WeightedPicker<ShishaPoopBehaviour.PoopType>(
            new List<(ShishaPoopBehaviour.PoopType item, float weight)>
            {
                (ShishaPoopBehaviour.PoopType.Common, ShishaPlugin.Config.CommonCrystalSpawnWeight),
                (ShishaPoopBehaviour.PoopType.Uncommon, ShishaPlugin.Config.UncommonCrystalSpawnWeight),
                (ShishaPoopBehaviour.PoopType.Rare, ShishaPlugin.Config.RareCrystalSpawnWeight)
            });
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
        InitializeConfigValues();

        base.Start();
        if (!IsServer) return;

        SubscribeToNetworkEvents();
        RegisterImperiumInsights();

        _numberOfAmbientAudioClips = GetComponent<ShishaClient>().ambientAudioClips.Length;
    }

    private void RegisterImperiumInsights()
    {
        bool isAgentNull = !_adapter.Agent;

        if (ImperiumIntegration.IsLoaded && !_hasRegisteredImperiumInsights)
        {
            Imperium.API.Visualization.InsightsFor<ShishaServer>()
                .SetPersonalNameGenerator(entity => entity.Id)
                .RegisterInsight("Behaviour State", entity => entity.CurrentState.GetStateType().ToString())
                .RegisterInsight("Acceleration",
                    entity => !isAgentNull ? $"{entity._adapter.Agent.acceleration:0.0}" : "0")
                .RegisterInsight("Gender", entity => entity._blackboard.Gender.ToString());
        }

        _hasRegisteredImperiumInsights = true;
    }

    public void ManageAmbientSfx()
    {
        if (!_blackboard.IsWanderEnabled) return;

        _ambientAudioTimer -= Time.deltaTime;
        if (_ambientAudioTimer <= 0)
        {
            _ambientAudioTimer = Random.Range(ShishaPlugin.Config.AmbientSfxTimerMin, ShishaPlugin.Config.AmbientSfxTimerMax);
            if (_numberOfAmbientAudioClips == 0) return;
            netcodeController.PlayAmbientSfxClientRpc(Random.Range(0, _numberOfAmbientAudioClips));
        }
    }

    protected override bool ShouldRunUpdate()
    {
        return IsServer && !_adapter.IsDead;
    }

    protected override bool ShouldRunAiInterval()
    {
        return ShouldRunUpdate();
    }

    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }

    public override void DaytimeEnemyLeave()
    {
        if (!IsServer || !_blackboard.IsTimeInDayLeaveEnabled) return;
        StartCoroutine(LeaveWhenNoOneIsLooking(Random.Range(5f, 12f)));
        base.DaytimeEnemyLeave();
    }

    private IEnumerator LeaveWhenNoOneIsLooking(float checkIntervalTimer)
    {
        if (!IsServer) yield break;

        while (!_adapter.IsDead)
        {
            if (!IsPlayerLookingAtShisha(120f, 80))
            {
                // Doesn't transition to the dead state because we just want the shisha to vanish and appear like its left the map
                KillEnemyServerRpc(false);
                Destroy(gameObject);
                yield break;
            }

            yield return new WaitForSeconds(checkIntervalTimer);
        }
    }

    private bool IsPlayerLookingAtShisha(
        float playerViewWidth = 30f,
        int playerViewRange = 60,
        float playerProximityAwareness = 3f)
    {
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (!PlayerUtil.IsPlayerDead(player))
            {
                if (player.HasLineOfSightToPosition(eye.position, playerViewWidth, playerViewRange, playerProximityAwareness))
                    return true;
            }
        }

        return false;
    }

    private void HandleOneShotIdleAnimationComplete()
    {
        if (!IsServer) return;
        TriggerCustomEvent("OneShotIdleAnimationComplete");
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer || _adapter.IsDead || !_blackboard.IsKillable) return;

        // Hit cooldown
        if (Time.time - _lastHitTime < 0.02f)
            return;

        _lastHitTime = Time.time;
        _adapter.TargetPlayer = playerWhoHit;

        bool isNowDead = _adapter.ApplyDamage(force);
        if (isNowDead)
        {
            SwitchBehaviourState(States.Dead);
        }
        else
        {
            _blackboard.NetcodeController.SetAnimationTriggerClientRpc(ShishaClient.OnHit);
            if (CurrentState.GetStateType() != States.RunningAway)
            {
                SwitchBehaviourState(States.RunningAway);
            }
        }
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        LogVerbose("Initializing config values...");

        ShishaConfig config = ShishaPlugin.Config;

        RoamSearchRoutine.loopSearch = true;
        RoamSearchRoutine.searchWidth = config.WanderRadius;
        creatureVoice.volume = config.AmbientSfxVolume * 2;
        creatureSFX.volume = config.FootstepSfxVolume;
        _adapter.Health = Mathf.Max(config.Health, 1);

        _blackboard.IsKillable = config.Killable;
        _blackboard.IsWanderEnabled = config.Wander;
        _blackboard.IsAnchoredWanderEnabled = config.AnchoredWandering;
        _blackboard.IsTimeInDayLeaveEnabled = config.TimeInDayLeaveEnabled;
        _blackboard.IsPoopBehaviourEnabled = config.PoopBehaviourEnabled;
        _blackboard.PoopProbability = config.PoopChance;
        _blackboard.PoopPlaceholder = poopPlaceholder;
        _blackboard.Gender = Gender.Male;
        _blackboard.NetcodeController = netcodeController;
    }

    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;

        netcodeController.OnOneShotIdleAnimationComplete += HandleOneShotIdleAnimationComplete;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_networkEventsSubscribed) return;

        netcodeController.OnOneShotIdleAnimationComplete -= HandleOneShotIdleAnimationComplete;

        _networkEventsSubscribed = false;
    }

    protected override string GetLogPrefix()
    {
        return $"[ShishaServerAI {Id}]";
    }
}
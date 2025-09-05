using System.Collections;
using GameNetcodeStuff;
using LethalCompanyShisha.Core;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha;

public class ShishaServer : BaseAI
{
    internal enum States
    {
        Roaming,
        Idle,
        RunningAway,
        Dead,
    }

#pragma warning disable 0649
    [SerializeField] private AISearchRoutine roamSearchRoutine;
    [SerializeField] private Transform poopPlaceholder;

    [SerializeField] public ShishaNetcodeController netcodeController;
#pragma warning restore 0649

    private float _ambientAudioTimer;
    private float _takeDamageCooldown;
    private int _numberOfAmbientAudioClips;
    private bool _networkEventsSubscribed;

    public AIContext<ShishaBlackboard, ShishaAdapter> Context { get; private set; }
    private ShishaBlackboard _blackboard => Context.Blackboard;
    private ShishaAdapter _adapter => Context.Adapter;

    private void Awake()
    {
        ShishaBlackboard blackboard = new();
        ShishaAdapter adapter = new(this);

        Context = new AIContext<ShishaBlackboard, ShishaAdapter>(blackboard, adapter);
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
        base.Start();
        if (!IsServer) return;

        SubscribeToNetworkEvents();
        InitializeConfigValues();

        allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
        _numberOfAmbientAudioClips = GetComponent<ShishaClient>().ambientAudioClips.Length;

        _blackboard.SpawnPosition = transform.position;

        if (_blackboard.IsWanderEnabled) InitializeState((int)States.Roaming);

        LogDebug("Shisha spawned!");
    }

    public override void Update()
    {
        if (isEnemyDead) return;
        base.Update();
        if (!IsServer) return;

        _takeDamageCooldown -= Time.deltaTime;

        if (!_blackboard.IsWanderEnabled) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                _blackboard.WanderTimer -= Time.deltaTime;
                if (_blackboard.WanderTimer <= 0)
                {
                    SwitchBehaviourState((int)States.Idle);
                    break;
                }

                _adapter.MoveAgent();
                break;
            }

            case (int)States.Idle:
            {
                _adapter.MoveAgent();
                _adapter.Transform.position = _blackboard.IdlePosition;

                break;
            }

            case (int)States.RunningAway:
            {
                _adapter.MoveAgent();

                break;
            }
        }

        _ambientAudioTimer -= Time.deltaTime;
        if (_ambientAudioTimer <= 0)
        {
            _ambientAudioTimer = Random.Range(ShishaPlugin.Config.AmbientSfxTimerMin, ShishaPlugin.Config.AmbientSfxTimerMax);
            if (_numberOfAmbientAudioClips == 0) return;
            netcodeController.PlayAmbientSfxClientRpc(Random.Range(0, _numberOfAmbientAudioClips));
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;
        if (isEnemyDead) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.RunningAway:
            {
                if ((transform.position - _blackboard.RunAwayTransform.position).sqrMagnitude <= 9)
                {
                    SwitchBehaviourState(_blackboard.IsWanderEnabled ? (int)States.Roaming : (int)States.Idle);
                }

                break;
            }
        }
    }

    public override void DaytimeEnemyLeave()
    {
        if (!IsServer || !_blackboard.IsTimeInDayLeaveEnabled) return;
        StartCoroutine(LeaveWhenNoOneIsLooking(Random.Range(5f, 12f)));
        base.DaytimeEnemyLeave();
    }

    private IEnumerator LeaveWhenNoOneIsLooking(float checkIntervalTimer)
    {
        while (true)
        {
            if (!IsPlayerLookingAtShisha(120f, 80))
            {
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
            if (!player.isPlayerDead)
            {
                if (player.HasLineOfSightToPosition(transform.position + Vector3.up * 0.5f, playerViewWidth, playerViewRange, playerProximityAwareness))
                    return true;
            }
        }

        return false;
    }

    private void InitializeState(int state)
    {
        LogDebug($"Initializing state: {state}");
        switch (state)
        {
            case (int)States.Roaming:
            {
                _adapter.SetMovementProfile(ShishaPlugin.Config.MaxSpeed, ShishaPlugin.Config.Acceleration);
                _blackboard.WanderTimer = Random.Range(ShishaPlugin.Config.WanderTimeMin, ShishaPlugin.Config.WanderTimeMax);

                StartSearch(_blackboard.IsAnchoredWanderEnabled ? _blackboard.SpawnPosition : _adapter.Transform.position, roamSearchRoutine);
                break;
            }

            case (int)States.Idle:
            {
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);

                _adapter.BeginGracefulStop();

                _blackboard.IdlePosition = transform.position;

                PickRandomIdleAnimation();

                break;
            }

            case (int)States.RunningAway:
            {
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);

                _adapter.SetMovementProfile(ShishaPlugin.Config.RunningAwayMaxSpeed, ShishaPlugin.Config.Acceleration);

                break;
            }

            case (int)States.Dead:
            {
                netcodeController.SetAnimationBoolClientRpc(ShishaClient.IsDead, true);
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);

                _adapter.StopAllPathing();
                _adapter.KillAllSpeed();

                KillEnemyServerRpc(false);

                break;
            }
        }
    }

    public void HandleIdleCompleteStateBehaviourCallback()
    {
        if (!IsServer) return;

        SwitchBehaviourState((int)States.Roaming);
        netcodeController.SetAnimationTriggerClientRpc(ShishaClient.ForceWalk);
    }

    private void PickRandomIdleAnimation()
    {
        if (!IsServer || !_blackboard.IsPoopBehaviourEnabled) return;

        float randomValue = Random.value;
        LogVerbose($"Random value to determine chance of pooping: {randomValue}");

        if (randomValue < _blackboard.PoopProbability)
        {
            SpawnShishaPoop();
            netcodeController.SetAnimationTriggerClientRpc(ShishaClient.Poo);
        }
        else
        {
            int animationToPlay = Random.Range(1, 3);
            int animationIdToPlay = animationToPlay switch
            {
                1 => ShishaClient.Idle1,
                2 => ShishaClient.Idle2,
                _ => 0,
            };

            if (animationIdToPlay == 0)
            {
                LogVerbose($"Unable to play animation with random number: {animationToPlay}");
                return;
            }

            LogVerbose($"Playing animation with id: ({animationToPlay}, {animationIdToPlay})");
            netcodeController.SetAnimationTriggerClientRpc(animationIdToPlay);
        }
    }

    private void SpawnShishaPoop()
    {
        if (!IsServer) return;

        if (!poopPlaceholder)
        {
            ShishaPlugin.Logger.LogError("The poop placeholder transform is null, this should never happen.");
            return;
        }

        int variantIndex = GetRandomVariantIndex();

        GameObject poopObject = Instantiate(
            GetPoopItemFromIndex(variantIndex).spawnPrefab,
            poopPlaceholder.position,
            poopPlaceholder.rotation,
            poopPlaceholder);

        ShishaPoopBehaviour poopBehaviour = poopObject.GetComponent<ShishaPoopBehaviour>();
        int scrapValue = CalculateScrapValue(variantIndex);
        poopBehaviour.SetScrapValue(scrapValue);

        poopObject.GetComponent<NetworkObject>().Spawn();
        netcodeController.SpawnShishaPoopClientRpc(poopObject, scrapValue);
    }

    // Old function
    private static int GetRandomVariantIndex()
    {
        int commonCrystalChance = ShishaConfig.Instance.CommonCrystalChance.Value;
        int uncommonCrystalChance = ShishaConfig.Instance.UncommonCrystalChance.Value;
        int rareCrystalChance = ShishaConfig.Instance.RareCrystalChance.Value;

        if (commonCrystalChance + uncommonCrystalChance + rareCrystalChance != 100)
        {
            commonCrystalChance = 65;
            uncommonCrystalChance = 25;
            rareCrystalChance = 10;
        }

        int chosenVariantIndex;
        int roll = Random.Range(1, 101);

        if (roll <= commonCrystalChance) chosenVariantIndex = 0;
        else if (roll <= commonCrystalChance + uncommonCrystalChance) chosenVariantIndex = 1;
        else chosenVariantIndex = 2;

        return chosenVariantIndex;
    }

    // Make this one function in the future
    private static Item GetPoopItemFromIndex(int variantIndex)
    {
        return variantIndex switch
        {
            0 => ShishaPlugin.ShishaRedPoopItem,
            1 => ShishaPlugin.ShishaGreenPoopItem,
            2 => ShishaPlugin.ShishaBluePoopItem,
            _ => ShishaPlugin.ShishaRedPoopItem
        };
    }

    private static int CalculateScrapValue(int variant)
    {
        return variant switch
        {
            0 => Random.Range(ShishaConfig.Instance.CommonCrystalMinValue.Value,
                ShishaConfig.Instance.CommonCrystalMaxValue.Value + 1),
            1 => Random.Range(ShishaConfig.Instance.UncommonCrystalMinValue.Value,
                ShishaConfig.Instance.UncommonCrystalMaxValue.Value + 1),
            2 => Random.Range(ShishaConfig.Instance.RareCrystalMinValue.Value,
                ShishaConfig.Instance.RareCrystalMaxValue.Value + 1),
            _ => 1
        };
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer) return;
        if (isEnemyDead || currentBehaviourStateIndex == (int)States.Dead || !_blackboard.IsKillable) return;
        if (_takeDamageCooldown > 0) return;

        enemyHP -= force;
        _takeDamageCooldown = 0.03f;

        if (enemyHP > 0)
        {
            _blackboard.RunAwayTransform = GetFarthestValidNodeFromPosition(out PathStatus pathStatus,
                _adapter.Agent,
                !playerWhoHit ? transform.position : playerWhoHit.transform.position,
                allAINodes
            );

            if (pathStatus == PathStatus.Invalid) SwitchBehaviourState((int)States.Roaming);
            else
            {
                SetDestinationToPosition(_blackboard.RunAwayTransform.position);
                SwitchBehaviourState((int)States.RunningAway);
            }
        }
        else
        {
            _adapter.TargetPlayer = playerWhoHit;
            SwitchBehaviourState((int)States.Dead);
        }
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        roamSearchRoutine.loopSearch = true;
        roamSearchRoutine.searchWidth = ShishaPlugin.Config.WanderRadius;
        creatureVoice.volume = ShishaPlugin.Config.AmbientSfxVolume * 2;
        creatureSFX.volume = ShishaPlugin.Config.FootstepSfxVolume * 2;
        enemyHP = Mathf.Max(ShishaPlugin.Config.Health, 1);
    }

    private void SwitchBehaviourState(int state)
    {
        if (currentBehaviourStateIndex == state) return;
        LogDebug($"Switching to behaviour state {state}.");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        netcodeController.CurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
        InitializeState(state);
        LogDebug($"Switch to behaviour state {state} complete!");
    }

    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;

        netcodeController.OnIdleCompleteStateBehaviourCallback += HandleIdleCompleteStateBehaviourCallback;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_networkEventsSubscribed) return;

        netcodeController.OnIdleCompleteStateBehaviourCallback -= HandleIdleCompleteStateBehaviourCallback;

        _networkEventsSubscribed = false;
    }
}
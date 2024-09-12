using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha;

public class ShishaServer : EnemyAI
{
    private ManualLogSource _mls;
    private string _shishaId;

    private enum PathStatus
    {
        Invalid, // Path is invalid or incomplete
        ValidButInLos, // Path is valid but obstructed by line of sight
        Valid, // Path is valid and unobstructed
        Unknown,
    }

    public enum States
    {
        Roaming,
        Idle,
        RunningAway,
        Dead,
    }
    
#pragma warning disable 0649
    [SerializeField] private AISearchRoutine roamSearchRoutine;
    [SerializeField] private Transform poopPlaceholder;
#pragma warning restore 0649

    [Header("Movement")]
    [Tooltip("Toggle whether the creature is allowed to wander or not.")]
    public bool wanderEnabled = true;
    [Tooltip("When enabled, the creature will only wander around its spawn point within a radius defined by the Wander Radius. If disabled, the creature can wander from any point within the Wander Radius.")]
    public bool anchoredWandering = true;
    [Tooltip("The maximum speed of the creature.")]
    public float maxSpeed = 4f;
    [Tooltip("The maximum acceleration of the creature.")]
    public float maxAcceleration = 5f;
    [Tooltip("The creature will wander for a random duration within this range before going to an idle state.")]
    public Vector2 wanderTimeRange = new(5f, 45f);
    public Vector2 ambientSfxTimerRange = new(7.5f, 40f);
    public float poopChance = 0.05f;
    public float runningAwayMaxSpeed = 4f;
    public float runningAwayMaxAcceleration = 8f;
    
    public const ulong NullPlayerId = 69420;
    
    private ShishaNetcodeController _netcodeController;

    private float _wanderTimer;
    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _ambientAudioTimer;
    private float _takeDamageCooldown;

    private int _numberOfAmbientAudioClips;

    private bool _poopBehaviourEnabled;
    private bool _killable;
    private bool _networkEventsSubscribed;

    private Vector3 _idlePosition;
    private Vector3 _spawnPosition;

    private Transform _runAwayTransform;

    private readonly NullableObject<PlayerControllerB> _actualTargetPlayer = new();

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
        
        _shishaId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Server {_shishaId}");

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (creatureAnimator == null) creatureAnimator = GetComponent<Animator>();
        _netcodeController = GetComponent<ShishaNetcodeController>();
        if (_netcodeController != null) SubscribeToNetworkEvents();
        else
        {
            _mls.LogError("Netcode controller is null, aborting spawn.");
            Destroy(gameObject);
            return;
        }
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + _shishaId.GetHashCode() - thisEnemyIndex);
        _netcodeController.SyncShishaIdentifierClientRpc(_shishaId);
        InitializeConfigValues();
        
        allAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
        _spawnPosition = transform.position;
        _numberOfAmbientAudioClips = GetComponent<ShishaClient>().ambientAudioClips.Length;
            
        if (wanderEnabled) InitializeState((int)States.Roaming);
        LogDebug("Shisha spawned!");
    }

    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        if (isEnemyDead) return;
        
        _takeDamageCooldown -= Time.deltaTime;
        
        if (!wanderEnabled) return;
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                _wanderTimer -= Time.deltaTime;
                if (_wanderTimer <= 0)
                {
                    SwitchBehaviourState((int)States.Idle);
                    break;
                }
                
                MoveWithAcceleration();
                break;
            }

            case (int)States.Idle:
            {
                agent.speed = 0f;
                transform.position = _idlePosition;
                
                break;
            }

            case (int)States.RunningAway:
            {
                MoveWithAcceleration();
                break;
            }
        }
        
        _ambientAudioTimer -= Time.deltaTime;
        if (_ambientAudioTimer <= 0)
        {
            _ambientAudioTimer = Random.Range(ambientSfxTimerRange.x, ambientSfxTimerRange.y);
            if (_numberOfAmbientAudioClips == 0) return; 
            _netcodeController.PlayAmbientSfxClientRpc(_shishaId, Random.Range(0, _numberOfAmbientAudioClips));
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
                if (Vector3.Distance(transform.position, _runAwayTransform.position) <= 3)
                {
                    SwitchBehaviourState(wanderEnabled ? (int)States.Roaming : (int)States.Idle);
                }
                
                break;
            }
        }
    }

    public override void DaytimeEnemyLeave()
    {
        if (!IsServer) return;
        if (!ShishaConfig.Instance.TimeInDayLeaveEnabled.Value) return;
        StartCoroutine(LeaveWhenNoOneIsLooking(10f));
        base.DaytimeEnemyLeave();
    }

    private IEnumerator LeaveWhenNoOneIsLooking(float checkIntervalTimer)
    {
        while (true)
        {
            if (!IsPlayerLookingAtShisha(120f, 80, 3f))
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
        return StartOfRound.Instance.allPlayerScripts.Where(player => !player.isPlayerDead)
            .Any(player => player.HasLineOfSightToPosition(
                transform.position + Vector3.up * 0.5f, 
                playerViewWidth,
                playerViewRange,
                playerProximityAwareness
                ));
    }
    
    private void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, _agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, _agentMaxAcceleration, accelerationAdjustment);
    }

    private void InitializeState(int state)
    {
        LogDebug($"Initializing state: {state}");
        switch (state)
        {
            case (int)States.Roaming:
            {
                _agentMaxSpeed = maxSpeed;
                _agentMaxAcceleration = maxAcceleration;
                _wanderTimer = Random.Range(wanderTimeRange.x, wanderTimeRange.y);
                
                StartSearch(anchoredWandering ? _spawnPosition : transform.position, roamSearchRoutine);
                break;
            }

            case (int)States.Idle:
            {
                agent.speed = 0f;
                agent.acceleration = 40f;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = maxAcceleration;
                moveTowardsDestination = false;
                _idlePosition = transform.position;
                
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
                PickRandomIdleAnimation();
                
                break;
            }

            case (int)States.RunningAway:
            {
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
                agent.speed *= 1.25f;
                agent.acceleration *= 1.25f;
                _agentMaxSpeed = runningAwayMaxSpeed;
                _agentMaxAcceleration = runningAwayMaxAcceleration;
                
                break;
            }

            case (int)States.Dead:
            {
                _netcodeController.SetAnimationBoolClientRpc(_shishaId, ShishaClient.IsDead, true);
                if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
                agent.speed = 0f;
                agent.acceleration = 100f;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 100f;
                isEnemyDead = true;
                moveTowardsDestination = false;
                KillEnemyServerRpc(false);
                
                break;
            }
        }
    }

    public void HandleIdleCompleteStateBehaviourCallback(string receivedShishaId)
    {
        if (!IsServer) return;
        if (_shishaId != receivedShishaId) return;

        SwitchBehaviourState((int)States.Roaming);
        _netcodeController.SetAnimationTriggerClientRpc(_shishaId, ShishaClient.ForceWalk);
    }
    
    private void PickRandomIdleAnimation()
    {
        if (!IsServer) return;

        float randomValue = Random.value;
        LogDebug($"Random value to determine chance of pooping: {randomValue}");
        if (randomValue < poopChance && _poopBehaviourEnabled)
        {
            SpawnShishaPoop();
            _netcodeController.SetAnimationTriggerClientRpc(_shishaId, ShishaClient.Poo);
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
                LogDebug($"Unable to play animation with random number: {animationToPlay}");
                return;
            }
        
            LogDebug($"Playing animation with id: ({animationToPlay}, {animationIdToPlay})");
            _netcodeController.SetAnimationTriggerClientRpc(_shishaId, animationIdToPlay);
        }
    }

    private void SpawnShishaPoop()
    {
        if (!IsServer) return;

        GameObject poopObject = Instantiate(
            ShishaPlugin.ShishaPoopItem.spawnPrefab,
            poopPlaceholder.position,
            poopPlaceholder.rotation,
            poopPlaceholder);

        int variantIndex = ShishaPoopBehaviour.GetRandomVariantIndex();
        ShishaPoopBehaviour poopBehaviour = poopObject.GetComponent<ShishaPoopBehaviour>();
        if (poopPlaceholder == null)
        {
            _mls.LogError("PoopBehaviour is null, this should not happen.");
            return;
        }

        poopBehaviour.isPartOfShisha = true;
        poopBehaviour.grabbableToEnemies = false;
        poopBehaviour.grabbable = false;
        poopBehaviour.fallTime = 1f;
        poopBehaviour.variantIndex = variantIndex;
        
        Tuple<int, int> scrapValueRange = poopBehaviour.variantIndex switch
        {
            0 => new Tuple<int, int>(
                ShishaConfig.Instance.CommonCrystalMinValue.Value,
                ShishaConfig.Instance.CommonCrystalMaxValue.Value + 1),

            1 => new Tuple<int, int>(
                ShishaConfig.Instance.UncommonCrystalMinValue.Value,
                ShishaConfig.Instance.UncommonCrystalMaxValue.Value + 1),

            2 => new Tuple<int, int>(
                ShishaConfig.Instance.RareCrystalMinValue.Value,
                ShishaConfig.Instance.RareCrystalMaxValue.Value + 1),
            
            _ => new Tuple<int, int>(1, 2) // Shouldn't ever happen
        };

        int scrapValue = Random.Range(scrapValueRange.Item1, scrapValueRange.Item2);
        LogDebug($"Scrap value: {scrapValue}, scrap value range: {scrapValueRange}");
        poopBehaviour.SetScrapValue(scrapValue);
        RoundManager.Instance.totalScrapValueInLevel += scrapValue;

        NetworkObject poopNetworkObject = poopObject.GetComponent<NetworkObject>();
        poopNetworkObject.Spawn();
        _netcodeController.SpawnShishaPoopClientRpc(_shishaId, poopNetworkObject, poopBehaviour.variantIndex, scrapValue);
    }
    
    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer) return;
        if (isEnemyDead || currentBehaviourStateIndex == (int)States.Dead || !_killable) return;
        if (_takeDamageCooldown > 0) return;
        
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        
        if (enemyHP > 0)
        {
            _runAwayTransform = GetFarthestValidNodeFromPosition(out PathStatus pathStatus,
                agent,
                playerWhoHit == null ? transform.position : playerWhoHit.transform.position,
                allAINodes
            );
            
            if (pathStatus == PathStatus.Invalid) SwitchBehaviourState((int)States.Roaming);
            else
            {
                SetDestinationToPosition(_runAwayTransform.position);
                SwitchBehaviourState((int)States.RunningAway);
            }
        }
        else
        {
            _netcodeController.TargetPlayerClientId.Value = playerWhoHit == null ? NullPlayerId : playerWhoHit.actualClientId;
            SwitchBehaviourState((int)States.Dead);
        }
    }
    
    /// <summary>
    /// Gets the farthest valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The transform of the farthest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    private static Transform GetFarthestValidNodeFromPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f,
        ManualLogSource logSource = null)
    {
        return GetValidNodeFromPosition(
            findClosest: false, 
            pathStatus:out pathStatus, 
            agent: agent, 
            position: position, 
            allAINodes: allAINodes, 
            ignoredAINodes: ignoredAINodes, 
            checkLineOfSight: checkLineOfSight, 
            allowFallbackIfBlocked: allowFallbackIfBlocked, 
            bufferDistance: bufferDistance, 
            logSource: logSource);
    }
    
    /// <summary>
    /// Gets a valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="findClosest">Whether to find the closest valid node (true) or the farthest valid node (false).</param>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The transform of the valid AI node that the agent can path to, or null if no valid node is found.</returns>
    private static Transform GetValidNodeFromPosition(
        bool findClosest,
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes,
        bool checkLineOfSight,
        bool allowFallbackIfBlocked,
        float bufferDistance,
        ManualLogSource logSource
        )
    {
        HashSet<GameObject> ignoredNodesSet = ignoredAINodes == null ? [] : [..ignoredAINodes];
        
        List<GameObject> aiNodes = allAINodes
            .Where(node => !ignoredNodesSet.Contains(node) && Vector3.Distance(position, node.transform.position) > bufferDistance)
            .ToList();
        
        aiNodes.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(position, a.transform.position);
            float distanceB = Vector3.Distance(position, b.transform.position);
            return findClosest ? distanceA.CompareTo(distanceB) : distanceB.CompareTo(distanceA);
        });

        foreach (GameObject node in aiNodes)
        {
            pathStatus = IsPathValid(agent, node.transform.position, checkLineOfSight, logSource: logSource);
            if (pathStatus == PathStatus.Valid)
            {
                return node.transform;
            }

            if (pathStatus == PathStatus.ValidButInLos && allowFallbackIfBlocked)
            {
                // Try to find another valid node without checking line of sight
                foreach (GameObject fallbackNode in aiNodes)
                {
                    if (fallbackNode == node) continue;
                    PathStatus fallbackStatus = IsPathValid(
                        agent, 
                        fallbackNode.transform.position,
                        logSource: logSource);

                    if (fallbackStatus == PathStatus.Valid)
                    {
                        pathStatus = PathStatus.ValidButInLos;
                        return fallbackNode.transform;
                    }
                }
            }
        }

        pathStatus = PathStatus.Invalid;
        return null;
    }
    
    /// <summary>
    /// Checks if the AI can construct a valid path to the given position.
    /// </summary>
    /// <param name="agent">The NavMeshAgent to construct the path for.</param>
    /// <param name="position">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="bufferDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns true if the agent can path to the position within the buffer distance or if a valid path exists; otherwise, false.</returns>
    private static PathStatus IsPathValid(
        NavMeshAgent agent, 
        Vector3 position, 
        bool checkLineOfSight = false, 
        float bufferDistance = 0f, 
        ManualLogSource logSource = null)
    {
        // Check if the desired location is within the buffer distance
        if (Vector3.Distance(agent.transform.position, position) <= bufferDistance)
        {
            //LogDebug(logSource, $"Target position {position} is within buffer distance {bufferDistance}.");
            return PathStatus.Valid;
        }
        
        NavMeshPath path = new();

        // Calculate path to the target position
        if (!agent.CalculatePath(position, path) || path.corners.Length == 0)
        {
            return PathStatus.Invalid;
        }

        // Check if the path is complete
        if (path.status != NavMeshPathStatus.PathComplete)
        {
            return PathStatus.Invalid;
        }

        // Check if any segment of the path is intersected by line of sight
        if (checkLineOfSight)
        {
            if (Vector3.Distance(path.corners[^1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5)
                return PathStatus.ValidButInLos;
            
            for (int i = 1; i < path.corners.Length; ++i)
            {
                if (Physics.Linecast(path.corners[i - 1], path.corners[i], 262144))
                {
                    return PathStatus.ValidButInLos;
                }
            }
        }

        return PathStatus.Valid;
    }

    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        
        float wanderTimeMin = Mathf.Clamp(ShishaConfig.Instance.WanderTimeMin.Value, 0f, 500f);
        float ambientSfxTimeMin = Mathf.Clamp(ShishaConfig.Instance.AmbientSfxTimerMin.Value, 0f, 500f);
        roamSearchRoutine.loopSearch = true;
        roamSearchRoutine.searchWidth = Mathf.Clamp(ShishaConfig.Instance.WanderRadius.Value, 50f, 500f);
        creatureVoice.volume = Mathf.Clamp(ShishaConfig.Default.AmbientSoundEffectsVolume.Value, 0, 1) * 2;
        creatureSFX.volume = Mathf.Clamp(ShishaConfig.Default.FootstepSoundEffectsVolume.Value, 0, 1) * 2;
        anchoredWandering = ShishaConfig.Instance.AnchoredWandering.Value;
        maxSpeed = Mathf.Clamp(ShishaConfig.Instance.MaxSpeed.Value, 0.1f, 100f);
        maxAcceleration = Mathf.Clamp(ShishaConfig.Instance.MaxAcceleration.Value, 0.1f, 100f);
        runningAwayMaxSpeed = Mathf.Clamp(ShishaConfig.Instance.RunningAwayMaxSpeed.Value, 0.1f, 100f);
        runningAwayMaxAcceleration = Mathf.Clamp(ShishaConfig.Instance.RunningAwayMaxAcceleration.Value, 0.1f, 100f);
        _poopBehaviourEnabled = ShishaConfig.Instance.PoopBehaviourEnabled.Value;
        poopChance = Mathf.Clamp(ShishaConfig.Instance.PoopChance.Value, 0f, 1f);
        _killable = ShishaConfig.Instance.Killable.Value;
        enemyHP = Mathf.Max(ShishaConfig.Instance.Health.Value, 1);
        wanderTimeRange = new Vector2(wanderTimeMin,
            Mathf.Clamp(ShishaConfig.Instance.WanderTimeMax.Value, wanderTimeMin, 1000f));
        ambientSfxTimerRange = new Vector2(ambientSfxTimeMin,
            Mathf.Clamp(ShishaConfig.Instance.AmbientSfxTimerMax.Value, ambientSfxTimeMin, 1000f));
    }

    private void SwitchBehaviourState(int state)
    {
        if (currentBehaviourStateIndex == state) return;
        LogDebug($"Switching to behaviour state {state}.");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        _netcodeController.CurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
        InitializeState(state);
        LogDebug($"Switch to behaviour state {state} complete!");
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        _actualTargetPlayer.Value = newValue == NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        targetPlayer = _actualTargetPlayer.Value;
        LogDebug(_actualTargetPlayer.IsNotNull
            ? $"Changed target player to {_actualTargetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;
        
        _netcodeController.OnIdleCompleteStateBehaviourCallback += HandleIdleCompleteStateBehaviourCallback;
        
        _netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;

        _networkEventsSubscribed = true;
    }

    
    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_networkEventsSubscribed) return;
        
        _netcodeController.OnIdleCompleteStateBehaviourCallback -= HandleIdleCompleteStateBehaviourCallback;
        
        _netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;

        _networkEventsSubscribed = false;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsServer) return;
        _mls?.LogInfo(msg);
        #endif
    }
}
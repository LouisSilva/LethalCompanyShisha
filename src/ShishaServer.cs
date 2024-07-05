using System;
using BepInEx.Logging;
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

    public enum States
    {
        Roaming,
        Idle,
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
    
    private ShishaNetcodeController _netcodeController;

    private float _wanderTimer;
    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _ambientAudioTimer;

    private int _numberOfAmbientAudioClips;

    private bool _poopBehaviourEnabled;

    private Vector3 _idlePosition;
    private Vector3 _spawnPosition;

    private void OnEnable()
    {
        if (_netcodeController == null) return;
        _netcodeController.OnIdleCompleteStateBehaviourCallback += HandleIdleCompleteStateBehaviourCallback;
    }

    private void OnDisable()
    {
        if (_netcodeController == null) return;
        _netcodeController.OnIdleCompleteStateBehaviourCallback += HandleIdleCompleteStateBehaviourCallback;
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        _shishaId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"Shisha Server {_shishaId}");

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (creatureAnimator == null) creatureAnimator = GetComponent<Animator>();
        _netcodeController = GetComponent<ShishaNetcodeController>();
        if (_netcodeController != null) OnEnable();
        else
        {
            _mls.LogError("Netcode controller is null, this is very bad");
            return;
        }
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + _shishaId.GetHashCode());
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
        if (!wanderEnabled) return;
        if (isEnemyDead) return;
        
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
        }
        
        _ambientAudioTimer -= Time.deltaTime;
        if (_ambientAudioTimer <= 0)
        {
            _ambientAudioTimer = Random.Range(ambientSfxTimerRange.x, ambientSfxTimerRange.y);
            if (_numberOfAmbientAudioClips == 0) return; 
            _netcodeController.PlayAmbientSfxClientRpc(_shishaId, Random.Range(0, _numberOfAmbientAudioClips));
        }
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
                
                _netcodeController.ChangeAnimationParameterBoolClientRpc(_shishaId, ShishaClient.IsWalking, true);
                StartSearch(anchoredWandering ? _spawnPosition : transform.position, roamSearchRoutine);
                break;
            }

            case (int)States.Idle:
            {
                agent.speed = 0f;
                agent.acceleration = 0f;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = maxAcceleration;
                moveTowardsDestination = false;
                _idlePosition = transform.position;
                
                _netcodeController.ChangeAnimationParameterBoolClientRpc(_shishaId, ShishaClient.IsWalking, false);
                _netcodeController.ChangeAnimationParameterBoolClientRpc(_shishaId, ShishaClient.IsRunning, false);
                StopSearch(roamSearchRoutine);
                PickRandomIdleAnimation();
                
                break;
            }
        }
    }

    public void HandleIdleCompleteStateBehaviourCallback(string receivedShishaId)
    {
        if (!IsServer) return;
        if (_shishaId != receivedShishaId) return;

        SwitchBehaviourState((int)States.Roaming);
        _netcodeController.DoAnimationClientRpc(_shishaId, ShishaClient.ForceWalk);
    }
    
    private void PickRandomIdleAnimation()
    {
        if (!IsServer) return;

        float randomValue = Random.value;
        LogDebug($"Random value to determine chance of pooping: {randomValue}");
        if (randomValue < poopChance && _poopBehaviourEnabled)
        {
            SpawnShishaPoop();
            _netcodeController.DoAnimationClientRpc(_shishaId, ShishaClient.Poo);
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
            _netcodeController.DoAnimationClientRpc(_shishaId, animationIdToPlay);
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

    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        
        float wanderTimeMin = Mathf.Clamp(ShishaConfig.Instance.WanderTimeMin.Value, 0f, 500f);
        float ambientSfxTimeMin = Mathf.Clamp(ShishaConfig.Instance.AmbientSfxTimerMin.Value, 0f, 500f);
        roamSearchRoutine.loopSearch = true;
        roamSearchRoutine.searchWidth = Mathf.Clamp(ShishaConfig.Instance.WanderRadius.Value, 1f, 500f);
        creatureVoice.volume = Mathf.Clamp(ShishaConfig.Default.AmbientSoundEffectsVolume.Value, 0, 1) * 2;
        creatureSFX.volume = Mathf.Clamp(ShishaConfig.Default.FootstepSoundEffectsVolume.Value, 0, 1) * 2;
        anchoredWandering = ShishaConfig.Instance.AnchoredWandering.Value;
        maxSpeed = Mathf.Clamp(ShishaConfig.Instance.MaxSpeed.Value, 0.1f, 100f);
        maxAcceleration = Mathf.Clamp(ShishaConfig.Instance.MaxAcceleration.Value, 0.1f, 100f);
        _poopBehaviourEnabled = ShishaConfig.Instance.PoopBehaviourEnabled.Value;
        poopChance = Mathf.Clamp(ShishaConfig.Instance.PoopChance.Value, 0f, 1f);
        wanderTimeRange = new Vector2(wanderTimeMin,
            Mathf.Clamp(ShishaConfig.Instance.WanderTimeMax.Value, wanderTimeMin, 1000f));
        ambientSfxTimerRange = new Vector2(ambientSfxTimeMin,
            Mathf.Clamp(ShishaConfig.Instance.AmbientSfxTimerMax.Value, ambientSfxTimeMin, 1000f));
        
        _netcodeController.InitializeConfigValuesClientRpc(_shishaId);
    }

    private void SwitchBehaviourState(int state)
    {
        if (currentBehaviourStateIndex == state) return;
        LogDebug($"Switching to behaviour state {state}");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        _netcodeController.ChangeBehaviourStateIndexClientRpc(_shishaId, state);
        InitializeState(state);
        LogDebug($"Switch to behaviour state {state} complete!");
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        if (!IsServer) return;
        _mls?.LogInfo(msg);
        #endif
    }
}
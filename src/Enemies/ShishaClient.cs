using LethalCompanyShisha.CustomStateMachineBehaviours;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha;

public class ShishaClient : MonoBehaviour
{
    private static readonly int IsRunning = Animator.StringToHash("Run");
    private static readonly int IsWalking = Animator.StringToHash("Walk");
    public static readonly int IsDead = Animator.StringToHash("Dead");
    public static readonly int ForceWalk = Animator.StringToHash("ForceWalk");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    private static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
    public static readonly int Idle1 = Animator.StringToHash("Idle1");
    public static readonly int Idle2 = Animator.StringToHash("Idle2");
    public static readonly int Poo = Animator.StringToHash("Poo");

#pragma warning disable 0649
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] private AudioSource creatureSfx;
    [SerializeField] private Renderer renderer;
    [SerializeField] private Transform poopPlaceholder;
    [SerializeField] private ParticleSystem poofParticleSystem;
    [SerializeField] private GameObject scanNode;

    [Header("Controllers")] [Space(5f)]
    [SerializeField] private ShishaNetcodeController netcodeController;
#pragma warning restore 0649

    [Header("Movement")] [Tooltip("The maximum speed that the creature can maintain while still walking")]
    public float walkSpeedThreshold = 2f;

    [Header("Audio")]
    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is wandering.")]
    public AudioClip[] ambientAudioClips;

    [Tooltip("The volume for ambient audio.")] [Range(0f, 2f)] public float ambientAudioVolume = 1f;

    [Space] [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is moving.")]
    public AudioClip[] walkingAudioClips;

    [Tooltip("The interval between playing walking audio clips.")] public float walkingAudioInterval = 0.5f;

    private Animator _animator;

    private ShishaPoopBehaviour _currentPoop;

    private Vector3 _agentLastPosition;

    private bool _networkEventsSubscribed;

    private const float MaxWalkAnimationSpeedMultiplier = 2;
    private float _agentCurrentSpeed;
    private float _walkingAudioTimer;

    private int _currentBehaviourStateIndex;
    private static readonly int GotHit = Animator.StringToHash("GotHit");

    private void Awake()
    {
        if (!netcodeController) netcodeController = GetComponent<ShishaNetcodeController>();
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void Start()
    {
        _animator = GetComponent<Animator>();

        InitializeConfigValues();
        AddStateMachineBehaviours(_animator);
    }

    private void Update()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime,
            0.75f);
        _agentLastPosition = position;

        switch (_currentBehaviourStateIndex)
        {
            case (int)ShishaServer.States.Roaming or (int)ShishaServer.States.RunningAway:
            {
                if (_agentCurrentSpeed <= walkSpeedThreshold && _agentCurrentSpeed > 0)
                {
                    _animator.SetBool(IsWalking, true);
                    _animator.SetBool(IsRunning, false);

                    float walkSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / walkSpeedThreshold, 0,
                        MaxWalkAnimationSpeedMultiplier);
                    _animator.SetFloat(WalkSpeed, walkSpeedMultiplier);
                }
                else if (_agentCurrentSpeed > walkSpeedThreshold)
                {
                    _animator.SetBool(IsWalking, true);
                    _animator.SetBool(IsRunning, true);

                    float runSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / 4f, 0, 5);
                    _animator.SetFloat(RunSpeed, runSpeedMultiplier);
                }
                else
                {
                    _animator.SetBool(IsWalking, false);
                    _animator.SetBool(IsRunning, false);
                }

                _walkingAudioTimer -= Time.deltaTime;
                if (walkingAudioClips != null && _walkingAudioTimer <= 0f)
                {
                    AudioClip audioClipToPlay = walkingAudioClips[Random.Range(0, walkingAudioClips.Length)];
                    creatureSfx.Stop(true);
                    creatureSfx.PlayOneShot(audioClipToPlay);
                    WalkieTalkie.TransmitOneShotAudio(creatureSfx, audioClipToPlay, creatureSfx.volume);

                    _walkingAudioTimer = walkingAudioInterval;
                }

                break;
            }
        }
    }

    private void HandlePlayAmbientSfx(int clipIndex)
    {
        AudioClip ambientAudioClipToPlay = ambientAudioClips[clipIndex];
        creatureVoice.PlayOneShot(ambientAudioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, ambientAudioClipToPlay, ambientAudioVolume);
        RoundManager.Instance.PlayAudibleNoise(creatureVoice.gameObject.transform.position);
    }

    private void HandleSpawnShishaPoop(NetworkObjectReference poopNetworkObjectReference, int scrapValue)
    {
        if (!poopNetworkObjectReference.TryGet(out NetworkObject poopNetworkObject)) return;
        ShishaPlugin.LogVerbose("Poop network object was not null!");

        _currentPoop = poopNetworkObject.GetComponent<ShishaPoopBehaviour>();
        _currentPoop.transform.position = poopPlaceholder.transform.position;
        _currentPoop.transform.rotation = poopPlaceholder.transform.rotation;
        _currentPoop.transform.SetParent(poopPlaceholder, false);
        _currentPoop.SetScrapValue(scrapValue);

        ShishaPlugin.LogVerbose("Shisha poop spawned");
    }

    public void OnAnimationEventDropShishaPoop()
    {
        if (!_currentPoop) return;

        _currentPoop.parentObject = null;
        _currentPoop.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _currentPoop.EnablePhysics(true);
        _currentPoop.FallToGround(true);
        _currentPoop.transform.SetParent(RoundManager.Instance.spawnedScrapContainer, true);
        _currentPoop.isHeld = false;
        _currentPoop.grabbable = true;
        _currentPoop.grabbableToEnemies = true;
    }

    public void OnAnimationEventDeathAnimationComplete()
    {
        StartCoroutine(CompleteDeathSequence());
    }

    private IEnumerator CompleteDeathSequence()
    {
        ShishaPlugin.LogVerbose($"In {nameof(CompleteDeathSequence)}");
        yield return new WaitForSeconds(1);

        poofParticleSystem.Play();
        renderer.enabled = false;
        creatureSfx.Stop(true);
        Destroy(scanNode.gameObject);
        yield return new WaitForSeconds(0.1f);

        Destroy(renderer.gameObject);
        if (!netcodeController.IsServer) yield break;

        SpawnDeathPoopsServerRpc();
        yield return new WaitForSeconds(0.5f);

        ShishaServer shishaServer = GetComponent<ShishaServer>();
        if (shishaServer) Destroy(shishaServer);
        Destroy(this);
    }

    [ServerRpc]
    private void SpawnDeathPoopsServerRpc()
    {
        List<Item> poopVariantsToSpawn = [ShishaPlugin.ShishaRedPoopItem, ShishaPlugin.ShishaGreenPoopItem, ShishaPlugin.ShishaBluePoopItem];

        foreach (Item poopVariant in poopVariantsToSpawn)
        {
            Vector3 poopPos = RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(transform.position, 2);
            GameObject poopObject = Instantiate(poopVariant.spawnPrefab, poopPos, Quaternion.identity,
                StartOfRound.Instance.propsContainer);
            ShishaPoopBehaviour poopBehaviour = poopObject.GetComponent<ShishaPoopBehaviour>();

            poopBehaviour.EnablePhysics(true);
            poopBehaviour.FallToGround(true);
            poopBehaviour.transform.SetParent(RoundManager.Instance.spawnedScrapContainer, true);

            NetworkObject poopNetworkObject = poopObject.GetComponent<NetworkObject>();
            poopNetworkObject.Spawn();
        }
    }

    private void InitializeConfigValues()
    {
        creatureVoice.volume = ShishaPlugin.Config.AmbientSfxVolume * 2;
        creatureSfx.volume = ShishaPlugin.Config.FootstepSfxVolume * 2;
    }

    private void AddStateMachineBehaviours(Animator receivedAnimator)
    {
        StateMachineBehaviour[] behaviours = receivedAnimator.GetBehaviours<StateMachineBehaviour>();
        foreach (StateMachineBehaviour behaviour in behaviours)
        {
            if (behaviour is BaseStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(netcodeController);
            }
        }
    }

    private void HandleBehaviourStateChanged(int oldValue, int newValue)
    {
        _currentBehaviourStateIndex = newValue;
        ShishaPlugin.LogVerbose($"Changed behaviour state to {newValue}");

        ShishaServer.States newState = (ShishaServer.States)newValue;
        switch (newState)
        {
            case ShishaServer.States.Roaming:
                _animator.SetBool(IsWalking, true);
                break;
            case ShishaServer.States.Idle:
                _animator.SetBool(IsWalking, false);
                _animator.SetBool(IsRunning, false);
                break;
            case ShishaServer.States.RunningAway:
                _animator.SetBool(IsWalking, true);
                _animator.SetTrigger(GotHit);
                break;
            case ShishaServer.States.Dead:
                _animator.SetBool(IsWalking, false);
                _animator.SetBool(IsRunning, false);
                _animator.SetBool(IsDead, true);
                break;
        }
    }

    private void HandleSetAnimationTrigger(int animationId)
    {
        _animator.SetTrigger(animationId);
    }

    private void HandleSetAnimationBool(int animationId, bool value)
    {
        _animator.SetBool(animationId, value);
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed || netcodeController) return;

        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop += HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx += HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool += HandleSetAnimationBool;

        netcodeController.CurrentBehaviourStateIndex.OnValueChanged += HandleBehaviourStateChanged;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed || netcodeController) return;

        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop -= HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx -= HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool -= HandleSetAnimationBool;

        netcodeController.CurrentBehaviourStateIndex.OnValueChanged -= HandleBehaviourStateChanged;

        _networkEventsSubscribed = false;
    }
}
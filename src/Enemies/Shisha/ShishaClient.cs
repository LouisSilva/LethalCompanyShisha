using LethalCompanyShisha.Enemies.CustomStateMachineBehaviours;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha.Enemies;

public class ShishaClient : MonoBehaviour
{
    public static readonly int ForceWalk = Animator.StringToHash("ForceWalk");
    public static readonly int OnHit = Animator.StringToHash("OnHit");
    public static readonly int IsDead = Animator.StringToHash("isDead");
    public static readonly int DoPoop = Animator.StringToHash("DoPoop");
    public static readonly int DoBow = Animator.StringToHash("DoBow");
    public static readonly int IsGrazing = Animator.StringToHash("IsGrazing");
    public static readonly int IsLyingDown = Animator.StringToHash("IsLyingDown");

#pragma warning disable 0649
    [SerializeField] private Renderer renderer;
    [SerializeField] private Transform poopPlaceholder;
    [SerializeField] private ParticleSystem poofParticleSystem;
    [SerializeField] private GameObject scanNode;

    [Header("Audio")]
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] private AudioSource creatureSfx;

    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is wandering.")]
    public AudioClip[] ambientAudioClips;

    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is moving.")]
    public AudioClip[] walkingAudioClips;

    [Tooltip("The interval between playing walking audio clips.")]
    public float walkingAudioInterval = 0.5f;

    [Header("Controllers")] [Space(5f)]
    [SerializeField] private ShishaNetcodeController netcodeController;
    [SerializeField] private Animator animator;
#pragma warning restore 0649

    private ShishaPoopBehaviour _currentPoop;

    private Vector3 _agentLastPosition;

    private bool _networkEventsSubscribed;

    private const float MaxWalkAnimationSpeedMultiplier = 2;
    private float _agentCurrentSpeed;
    private float _walkingAudioTimer;

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
        if (!animator) animator = GetComponent<Animator>();

        InitializeConfigValues();
        AddStateMachineBehaviours(animator);
    }

    private void Update()
    {
        // todo: fix this
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
                    animator.SetBool(IsWalking, true);
                    animator.SetBool(IsRunning, false);

                    float walkSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / walkSpeedThreshold, 0,
                        MaxWalkAnimationSpeedMultiplier);
                    animator.SetFloat(WalkSpeed, walkSpeedMultiplier);
                }
                else if (_agentCurrentSpeed > walkSpeedThreshold)
                {
                    animator.SetBool(IsWalking, true);
                    animator.SetBool(IsRunning, true);

                    float runSpeedMultiplier = Mathf.Clamp(_agentCurrentSpeed / 4f, 0, 5);
                    animator.SetFloat(RunSpeed, runSpeedMultiplier);
                }
                else
                {
                    animator.SetBool(IsWalking, false);
                    animator.SetBool(IsRunning, false);
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
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, ambientAudioClipToPlay);
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

        // ShishaServer shishaServer = GetComponent<ShishaServer>();
        // if (shishaServer) Destroy(shishaServer);
        // Destroy(this);
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

    private void HandleSetAnimationTrigger(int animationId)
    {
        animator.SetTrigger(animationId);
    }

    private void HandleSetAnimationBool(int animationId, bool value)
    {
        animator.SetBool(animationId, value);
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed || netcodeController) return;

        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop += HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx += HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool += HandleSetAnimationBool;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed || netcodeController) return;

        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop -= HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx -= HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool -= HandleSetAnimationBool;

        _networkEventsSubscribed = false;
    }
}
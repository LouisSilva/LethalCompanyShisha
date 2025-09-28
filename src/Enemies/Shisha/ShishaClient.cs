using LethalCompanyShisha.Enemies.CustomStateMachineBehaviours;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha.Enemies;

public class ShishaClient : MonoBehaviour
{
    public static readonly int ForceWalk = Animator.StringToHash("ForceWalk");
    public static readonly int OnHit = Animator.StringToHash("OnHit");
    public static readonly int IsDead = Animator.StringToHash("IsDead");
    public static readonly int DoPoop = Animator.StringToHash("DoPoop");
    public static readonly int IsBowing = Animator.StringToHash("IsBowing");
    public static readonly int IsGrazing = Animator.StringToHash("IsGrazing");
    public static readonly int IsLyingDown = Animator.StringToHash("IsLyingDown");
    private static readonly int Speed = Animator.StringToHash("Speed");

#pragma warning disable 0649
    [Header("Audio")]
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] private AudioSource creatureSfx;

    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is wandering.")]
    [SerializeField] public AudioClip[] ambientAudioClips;
    [SerializeField] public AudioClip[] walkingAudioClips;

    [Header("Renderers")]
    [SerializeField] private SkinnedMeshRenderer mainBodyRenderer;
    [SerializeField] private SkinnedMeshRenderer hornsRenderer;
    [SerializeField] private GameObject rootRenderer;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem poofParticleSystem;

    [Header("Misc Objects")]
    [SerializeField] private Transform poopPlaceholder;
    [SerializeField] private GameObject scanNode;

    [Header("Controllers")] [Space(5f)]
    [SerializeField] private ShishaNetcodeController netcodeController;
    [SerializeField] private Animator animator;
#pragma warning restore 0649

    private ShishaPoopBehaviour _currentPoop;

    private Vector3 _agentLastPosition;

    private bool _networkEventsSubscribed;

    private float _agentCurrentSpeed;

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

        SubscribeToNetworkEvents();
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
        animator.SetFloat(Speed, _agentCurrentSpeed);
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

        ShishaPlugin.LogVerbose("Shisha poop spawned.");
    }

    private void HandleSetGender(ShishaServer.Gender gender)
    {
        ShishaPlugin.LogVerbose($"[ShishaClient] Setting gender of this Shisha to {gender}.");

        bool shouldHornsRendererBeEnabled = hornsRenderer.enabled;
        Vector3 newScale = rootRenderer.transform.localScale;

        switch (gender)
        {
            case ShishaServer.Gender.Male:
            {
                shouldHornsRendererBeEnabled = true;
                newScale = new Vector3(0.9f, 0.9f, 0.9f);
                break;
            }

            case ShishaServer.Gender.Female:
            {
                shouldHornsRendererBeEnabled = false;
                newScale = new Vector3(0.8f, 0.8f, 0.8f);
                break;
            }
        }

        hornsRenderer.enabled = shouldHornsRendererBeEnabled;
        gameObject.transform.localScale = newScale;
    }

    public void DropPoop()
    {
        if (!_currentPoop) return;

        _currentPoop.parentObject = null;
        _currentPoop.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _currentPoop.EnablePhysics(true);
        _currentPoop.fallTime = 0f;

        Transform parent;
        _currentPoop.startFallingPosition =
            (parent = _currentPoop.transform.parent).InverseTransformPoint(_currentPoop.transform.position);
        _currentPoop.targetFloorPosition = parent.InverseTransformPoint(transform.position);
        _currentPoop.floorYRot = -1;
        _currentPoop.grabbable = true;
        _currentPoop.grabbableToEnemies = true;
        _currentPoop.isHeld = false;
        _currentPoop.isHeldByEnemy = false;
        _currentPoop = null;
    }

    public IEnumerator CompleteDeathSequence()
    {
        ShishaPlugin.LogVerbose($"In {nameof(CompleteDeathSequence)}");
        yield return new WaitForSeconds(1);

        poofParticleSystem.Play();
        mainBodyRenderer.enabled = false;
        creatureSfx.Stop(true);
        Destroy(scanNode.gameObject);
        yield return new WaitForSeconds(0.1f);

        Destroy(mainBodyRenderer.gameObject);
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
        creatureSfx.volume = ShishaPlugin.Config.FootstepSfxVolume;
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
        if (_networkEventsSubscribed || !netcodeController) return;

        ShishaPlugin.LogVerbose($"[ShishaClient] Subscribed to network events.");

        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop += HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx += HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool += HandleSetAnimationBool;
        netcodeController.OnSetGender += HandleSetGender;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed || !netcodeController) return;

        ShishaPlugin.LogVerbose($"[ShishaClient] Unsubscribed from network events.");

        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnSpawnShishaPoop -= HandleSpawnShishaPoop;
        netcodeController.OnPlayAmbientSfx -= HandlePlayAmbientSfx;
        netcodeController.OnSetAnimationBool -= HandleSetAnimationBool;
        netcodeController.OnSetGender -= HandleSetGender;

        _networkEventsSubscribed = false;
    }
}
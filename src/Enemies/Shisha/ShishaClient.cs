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
    public static readonly int DoLieDown = Animator.StringToHash("DoLieDown");
    private static readonly int Speed = Animator.StringToHash("Speed");

#pragma warning disable 0649
    [Header("Audio")]
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] public AudioSource creatureSfx;

    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is wandering.")]
    [SerializeField] public AudioClip[] ambientSfx;
    [SerializeField] public AudioClip[] footstepSfx;

    [Header("Renderers")]
    [SerializeField] private SkinnedMeshRenderer mainBodyRenderer;
    [SerializeField] private SkinnedMeshRenderer hornsRenderer;
    [SerializeField] private GameObject rootRenderer;
    [SerializeField] private List<SkinMaterialMap> skinMaterialMappings;

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem poofParticleSystem;

    [Header("Misc Objects")]
    [SerializeField] private Transform poopPlaceholder;
    [SerializeField] private GameObject scanNode;

    [Header("Controllers")] [Space(5f)]
    [SerializeField] private ShishaNetcodeController netcodeController;
    [SerializeField] private Animator animator;
#pragma warning restore 0649

    private Dictionary<ShishaSkinType, (Material skinMaterial, Material hornsMaterial)> _runtimeSkinMaterialMap;

    private ShishaPoopBehaviour _currentPoop;

    private Vector3 _agentLastPosition;

    private bool _networkEventsSubscribed;

    private float _agentCurrentSpeed;

    private void Awake()
    {
        if (!netcodeController) netcodeController = GetComponent<ShishaNetcodeController>();

        _runtimeSkinMaterialMap = new Dictionary<ShishaSkinType, (Material, Material)>(skinMaterialMappings.Count);

        foreach (SkinMaterialMap mapping in skinMaterialMappings)
        {
            if (_runtimeSkinMaterialMap.ContainsKey(mapping.skinType))
            {
                ShishaPlugin.LogVerbose($"[ShishaClient] Duplicate SkinType key found in material mappings: {mapping.skinType}. Ignoring duplicate.");
                continue;
            }

            _runtimeSkinMaterialMap.Add(mapping.skinType, (mapping.bodyMaterial, mapping.hornsMaterial));
        }
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
        AudioClip ambientAudioClipToPlay = ambientSfx[clipIndex];
        creatureVoice.PlayOneShot(ambientAudioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, ambientAudioClipToPlay);
        RoundManager.Instance.PlayAudibleNoise(creatureVoice.gameObject.transform.position);
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

    private void HandleSetSkinType(ShishaSkinType skinType)
    {
        ShishaPlugin.LogVerbose($"[ShishaClient] Setting the skin type of this Shisha to {skinType}.");

        if (skinType == ShishaSkinType.Default)
        {
            ShishaPlugin.Logger.LogWarning($"[ShishaClient] In {nameof(ShishaSkinType)}, the given skinType was Default.");
            skinType = ShishaSkinType.Default1;
        }

        (Material skinMaterial, Material hornsMaterial) materials = GetMaterialsForSkin(skinType);

        mainBodyRenderer.material = materials.skinMaterial;
        hornsRenderer.material = materials.hornsMaterial;
    }

    private (Material skinMaterial, Material hornsMaterial) GetMaterialsForSkin(ShishaSkinType skinType)
    {
        if (_runtimeSkinMaterialMap.TryGetValue(skinType, out (Material skinMaterial, Material hornsMaterial) materials))
        {
            return materials;
        }

        ShishaPlugin.Logger.LogWarning($"[ShishaClient] No material mapping found for skin type: {skinType}");
        return (null, null);
    }

    private void HandleSpawnShishaPoop(NetworkObjectReference poopNetworkObjectReference, int scrapValue)
    {
        if (!poopNetworkObjectReference.TryGet(out NetworkObject poopNetworkObject)) return;

        _currentPoop = poopNetworkObject.GetComponent<ShishaPoopBehaviour>();
        _currentPoop.transform.position = poopPlaceholder.transform.position;
        _currentPoop.transform.rotation = poopPlaceholder.transform.rotation;
        _currentPoop.transform.SetParent(poopPlaceholder, false);
        _currentPoop.SetScrapValue(scrapValue);

        ShishaPlugin.LogVerbose("[ShishaClient] Shisha poop spawned.");
    }

    public void DropPoop()
    {
        ShishaPlugin.LogVerbose($"[ShishaClient] Is _currentPoop null?: {_currentPoop == null}.");
        if (!_currentPoop) return;

        _currentPoop.parentObject = null;
        _currentPoop.transform.SetParent(null);
        _currentPoop.EnablePhysics(true);
        _currentPoop.fallTime = 0f;
        _currentPoop.startFallingPosition = _currentPoop.transform.parent.InverseTransformPoint(_currentPoop.transform.position);

        Vector3 targetWorldPosition = Physics.Raycast(_currentPoop.transform.position, Vector3.down, out RaycastHit hit, 200f, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore) ?
            hit.point : _currentPoop.transform.position;

        _currentPoop.targetFloorPosition = _currentPoop.transform.parent.InverseTransformPoint(targetWorldPosition);
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
        rootRenderer.gameObject.SetActive(false);
        creatureSfx.Stop(true);
        Destroy(scanNode.gameObject);
        yield return new WaitForSeconds(0.1f);

        Destroy(rootRenderer);
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
        netcodeController.OnSetSkinType += HandleSetSkinType;

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
        netcodeController.OnSetSkinType -= HandleSetSkinType;

        _networkEventsSubscribed = false;
    }
}
﻿using BepInEx.Logging;
using LethalCompanyShisha.CustomStateMachineBehaviours;
using System;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace LethalCompanyShisha;

public class ShishaClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _shishaId;
    
    public static readonly int IsRunning = Animator.StringToHash("Run");
    public static readonly int IsWalking = Animator.StringToHash("Walk");
    public static readonly int ForceWalk = Animator.StringToHash("ForceWalk");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    private static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
    public static readonly int Idle1 = Animator.StringToHash("Idle1");
    public static readonly int Idle2 = Animator.StringToHash("Idle2");
    public static readonly int Poo = Animator.StringToHash("Poo");
    
#pragma warning disable 0649
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] private AudioSource creatureSfx;
    [SerializeField] private Transform poopPlaceholder;
#pragma warning restore 0649
    
    [Header("Movement")]
    [Tooltip("The maximum speed that the creature can maintain while still walking")]
    public float walkSpeedThreshold = 2f;
    
    [Header("Audio")]
    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is wandering.")]
    public AudioClip[] ambientAudioClips;
    [Tooltip("The volume for ambient audio.")]
    [Range(0f, 2f)]
    public float ambientAudioVolume = 1f;
    [Space]
    [Tooltip("An array of audio clips that can be played randomly at intervals while the creature is moving.")]
    public AudioClip[] walkingAudioClips;
    [Tooltip("The interval between playing walking audio clips.")]
    public float walkingAudioInterval = 0.5f;
    
    [SerializeField] private float rayLength = 0.5f;
    
    private Animator _animator;
    
    private readonly NullableObject<ShishaNetcodeController> _netcodeController = new();
    
    private ShishaPoopBehaviour _currentPoop;
    
    private Vector3 _agentLastPosition;

    private bool _networkEventsSubscribed;
    
    private const float MaxWalkAnimationSpeedMultiplier = 2;
    private float _agentCurrentSpeed;
    private float _walkingAudioTimer;
    
    private int _currentBehaviourStateIndex;
    
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
        _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Client {_shishaId}");
        
        _animator = GetComponent<Animator>();
        _netcodeController.Value = GetComponent<ShishaNetcodeController>();
        
        if (_netcodeController != null) SubscribeToNetworkEvents();
        else
        {
            _mls.LogError("Netcode controller is null, this is very bad.");
            return;
        }

        InitializeConfigValues();
        AddStateMachineBehaviours(_animator);
    }

    private void Update()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;

        // AdjustRotationToSlope();

        switch (_currentBehaviourStateIndex)
        {
            case (int)ShishaServer.States.Roaming:
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

    private void HandlePlayAmbientSfx(string receivedShishaId, int clipIndex)
    {
        if (_shishaId != receivedShishaId) return;
        AudioClip ambientAudioClipToPlay = ambientAudioClips[clipIndex];
        creatureVoice.PlayOneShot(ambientAudioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, ambientAudioClipToPlay, ambientAudioVolume);
    }

    private void HandleSpawnShishaPoop(string receivedShishaId, NetworkObjectReference poopNetworkObjectReference,
        int variantIndex, int scrapValue)
    {
        if (_shishaId != receivedShishaId) return;
        if (!poopNetworkObjectReference.TryGet(out NetworkObject poopNetworkObject)) return;
        LogDebug("Poop network object was not null!");

        _currentPoop = poopNetworkObject.GetComponent<ShishaPoopBehaviour>();
        _currentPoop.isPartOfShisha = true;
        _currentPoop.transform.position = poopPlaceholder.transform.position;
        _currentPoop.transform.rotation = poopPlaceholder.transform.rotation;
        _currentPoop.transform.SetParent(poopPlaceholder, false);
        _currentPoop.grabbableToEnemies = false;
        _currentPoop.fallTime = 1f;
        _currentPoop.variantIndex = variantIndex;
        _currentPoop.SetScrapValue(scrapValue);
        _currentPoop.mainObjectRenderer.material = _currentPoop.poopMaterialVariants[variantIndex];
        _currentPoop.meshFilter.mesh = _currentPoop.poopMeshVariants[variantIndex];
        
        LogDebug("Shisha poop spawned");
    }

    public void OnAnimationEventDropShishaPoop()
    {
        if (_currentPoop == null) return;

        _currentPoop.isPartOfShisha = false;
        _currentPoop.parentObject = null;
        _currentPoop.heldByPlayerOnServer = false;
        _currentPoop.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _currentPoop.EnablePhysics(true);
        _currentPoop.isHeld = false;
        _currentPoop.isPocketed = false;
        _currentPoop.grabbable = true;
        _currentPoop.grabbableToEnemies = true;
        _currentPoop.startFallingPosition =
            _currentPoop.transform.parent.InverseTransformPoint(_currentPoop.transform.position);
        _currentPoop.FallToGround(true);
        _currentPoop.fallTime = Random.Range(-0.3f, 0.05f);
        
    }
    
    private void AdjustRotationToSlope()
    {
        const float rotationSpeed = 3f;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength)) return;
        
        Vector3 normal = hit.normal;
        Vector3 forward = Vector3.Cross(transform.right, normal);
        Quaternion slopeRotation = Quaternion.LookRotation(forward, normal);
        transform.rotation = Quaternion.Slerp(transform.rotation, slopeRotation, Time.deltaTime * rotationSpeed);
    }

    private void InitializeConfigValues()
    {
        creatureVoice.volume = Mathf.Clamp(ShishaConfig.Default.AmbientSoundEffectsVolume.Value, 0, 1) * 2;
        creatureSfx.volume = Mathf.Clamp(ShishaConfig.Default.FootstepSoundEffectsVolume.Value, 0, 1) * 2;
    }
    
    private void AddStateMachineBehaviours(Animator receivedAnimator)
    {
        StateMachineBehaviour[] behaviours = receivedAnimator.GetBehaviours<StateMachineBehaviour>();
        foreach (StateMachineBehaviour behaviour in behaviours)
        {
            if (behaviour is BaseStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(_netcodeController.Value);
            }
        }
    }

    private void HandleBehaviourStateChanged(int oldValue, int newValue)
    {
        _currentBehaviourStateIndex = newValue;
        LogDebug($"Changed behaviour state to {newValue}");

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
        }
    }

    private void HandleSetAnimationTriggerTrigger(string receivedShishaId, int animationId)
    {
        if (_shishaId != receivedShishaId) return;
        _animator.SetTrigger(animationId);
    }

    private void HandleSyncShishaIdentifier(string receivedShishaId)
    {
        _shishaId = receivedShishaId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"{ShishaPlugin.ModGuid} | Shisha Client {_shishaId}");

        LogDebug("Successfully synced shisha identifier");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed || !_netcodeController.IsNotNull) return;
        
        _netcodeController.Value.OnSyncShishaIdentifier += HandleSyncShishaIdentifier;
        _netcodeController.Value.OnSetAnimationTrigger += HandleSetAnimationTriggerTrigger;
        _netcodeController.Value.OnSpawnShishaPoop += HandleSpawnShishaPoop;
        _netcodeController.Value.OnPlayAmbientSfx += HandlePlayAmbientSfx;

        _netcodeController.Value.CurrentBehaviourStateIndex.OnValueChanged += HandleBehaviourStateChanged;

        _networkEventsSubscribed = true;
    }
    
    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed || !_netcodeController.IsNotNull) return;
        
        _netcodeController.Value.OnSyncShishaIdentifier -= HandleSyncShishaIdentifier;
        _netcodeController.Value.OnSetAnimationTrigger -= HandleSetAnimationTriggerTrigger;
        _netcodeController.Value.OnSpawnShishaPoop -= HandleSpawnShishaPoop;
        _netcodeController.Value.OnPlayAmbientSfx -= HandlePlayAmbientSfx;
        
        _netcodeController.Value.CurrentBehaviourStateIndex.OnValueChanged -= HandleBehaviourStateChanged;

        _networkEventsSubscribed = false;
    }
    
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}
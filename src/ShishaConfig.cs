using BepInEx.Configuration;
using LethalCompanyShisha.Core;
using System;
using UnityEngine;

namespace LethalCompanyShisha;

[Serializable]
public class ShishaConfig(ConfigFile cfg) : ConfigLoader<ShishaConfig>(cfg)
{
    [field: Tooltip("Whether to log more debug information to the console. 99% of people do NOT need to touch this.")]
    public bool VerboseLoggingEnabled { get; private set; } = false;

    #region Shisha Spawn Settings
    [field: Header("Shisha Spawn Settings")]

    [field: Tooltip("Whether the Shisha will spawn in games.")]
    public bool ShishaEnabled { get; private set; } = true;

    [field: Tooltip("Spawn weight of the Shisha on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "All:30";

    [field: Tooltip("The power level of the Shisha.")]
    [field: Range(0f, 100f)]
    public float PowerLevel { get; private set; } = 0.5f;

    [field: Tooltip("The max amount of Shishas that can spawn in the map.")]
    [field: Range(0, 500)]
    public int MaxAmount { get; private set; } = 20;
    #endregion

    #region Shisha Poop Spawn Settings
    [field: Header("Shisha Poop Settings")]

    [field: Tooltip("Toggles whether the Shisha can poop when idle.")]
    public bool PoopBehaviourEnabled { get; private set; } = true;

    [field: Tooltip("The chance of the Shisha pooping while idle. The setting PoopBehaviourEnabled must be set to true for this to work.")]
    [field: Range(0f, 1f)]
    public float PoopChance { get; private set; } = 0.05f;

    [field: Tooltip("")]
    [field: Range(0f, 999999f)]
    public float CommonCrystalSpawnWeight { get; private set; } = 65f;

    [field: Tooltip("")]
    [field: Range(0f, 999999f)]
    public float UncommonCrystalSpawnWeight { get; private set; } = 25f;

    [field: Tooltip("")]
    [field: Range(0f, 999999f)]
    public float RareCrystalSpawnWeight { get; private set; } = 10f;

    [field: Tooltip("The minimum value that the common crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int CommonCrystalMinValue { get; private set; } = 20;

    [field: Tooltip("The maximum value that the common crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int CommonCrystalMaxValue { get; private set; } = 35;

    [field: Tooltip("The minimum value that the uncommon crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int UncommonCrystalMinValue { get; private set; } = 40;

    [field: Tooltip("The maximum value that the uncommon crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int UncommonCrystalMaxValue { get; private set; } = 75;

    [field: Tooltip("The minimum value that the rare crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int RareCrystalMinValue { get; private set; } = 80;

    [field: Tooltip("The maximum value that the rare crystal can spawn with.")]
    [field: Range(0f, 999999f)]
    public int RareCrystalMaxValue { get; private set; } = 100;
    #endregion

    #region General Settings
    [field: Header("General Settings")]

    [field: Tooltip("Whether the Shisha can be killed.")]
    public bool Killable { get; private set; } = true;

    [field: Tooltip("The amount of health the Shisha has.")]
    [field: Range(1f, 999999f)]
    public int Health { get; private set; } = 3;

    [field: Tooltip("Toggles whether the Shisha will leave the map when it gets dark (like other vanilla daytime entities).")]
    public bool TimeInDayLeaveEnabled { get; private set; } = true;
    #endregion

    #region Movement Settings
    [field: Header("Movement Settings")]

    [field: Tooltip("The maximum distance from the Shisha's current position within which it can wander.")]
    [field: Range(0f, 999999f)]
    public float WanderRadius { get; private set; } = 50f;

    [field: Tooltip("When enabled, the Shisha will only wander around its spawn point within the radius defined by the Wander Radius setting. If disabled, the Shisha can wander from any point within the Wander Radius.")]
    public bool AnchoredWandering { get; private set; } = true;

    [field: Tooltip("The minimum time that the Shisha will wander for.")]
    public float WanderTimeMin { get; private set; } = 5f;

    [field: Tooltip("The maximum time that the Shisha will wander for.")]
    public float WanderTimeMax { get; private set; } = 45f;

    [field: Tooltip("The maximum speed of the Shisha.")]
    [field: Range(0f, 500f)]
    public float MaxSpeed { get; private set; } = 4f;

    [field: Tooltip("The acceleration of the Shisha.")]
    [field: Range(0f, 500f)]
    public float Acceleration { get; private set; } = 1f;

    [field: Tooltip("The maximum speed of the Shisha when running away from something.")]
    [field: Range(0f, 500f)]
    public float RunningAwayMaxSpeed { get; private set; } = 7f;
    #endregion

    #region Audio Settings
    [field: Header("Audio Settings")]

    [field: Tooltip("The minimum time gap between any given ambient sound effect.")]
    [field: Range(0f, 50f)]
    public float AmbientSfxTimerMin { get; private set; } = 7.5f;

    [field: Tooltip("The maximum time gap between any given ambient sound effect.")]
    [field: Range(0f, 500f)]
    public float AmbientSfxTimerMax { get; private set; } = 30f;

    [field: Tooltip("The volume of the ambient sounds of the Shisha.")]
    [field: Range(0f, 1f)]
    public float AmbientSfxVolume { get; private set; } = 0.4f;

    [field: Tooltip("The volume of the footstep sounds of the Shisha.")]
    [field: Range(0f, 1f)]
    public float FootstepSfxVolume { get; private set; } = 0.7f;
    #endregion
}
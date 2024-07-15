using BepInEx.Configuration;

namespace LethalCompanyShisha;

public class ShishaConfig : SyncedInstance<ShishaConfig>
{
    public readonly ConfigEntry<bool> ShishaEnabled;
    public readonly ConfigEntry<string> ShishaSpawnRarity;
    public readonly ConfigEntry<int> ShishaMaxAmount;
    public readonly ConfigEntry<float> ShishaPowerLevel;
    public readonly ConfigEntry<int> CommonCrystalChance;
    public readonly ConfigEntry<int> UncommonCrystalChance;
    public readonly ConfigEntry<int> RareCrystalChance;
    public readonly ConfigEntry<int> CommonCrystalMinValue;
    public readonly ConfigEntry<int> CommonCrystalMaxValue;
    public readonly ConfigEntry<int> UncommonCrystalMinValue;
    public readonly ConfigEntry<int> UncommonCrystalMaxValue;
    public readonly ConfigEntry<int> RareCrystalMinValue;
    public readonly ConfigEntry<int> RareCrystalMaxValue;
    public readonly ConfigEntry<float> AmbientSoundEffectsVolume;
    public readonly ConfigEntry<float> FootstepSoundEffectsVolume;

    public readonly ConfigEntry<float> WanderRadius;
    public readonly ConfigEntry<bool> AnchoredWandering;
    public readonly ConfigEntry<float> MaxSpeed;
    public readonly ConfigEntry<float> MaxAcceleration;
    public readonly ConfigEntry<float> WanderTimeMin;
    public readonly ConfigEntry<float> WanderTimeMax;
    public readonly ConfigEntry<float> AmbientSfxTimerMin;
    public readonly ConfigEntry<float> AmbientSfxTimerMax;
    public readonly ConfigEntry<bool> TimeInDayLeaveEnabled;
    public readonly ConfigEntry<bool> PoopBehaviourEnabled;
    public readonly ConfigEntry<float> PoopChance;

    public ShishaConfig(ConfigFile cfg)
    {
        InitInstance(this);

        WanderRadius = cfg.Bind(
            "General",
            "Wander Radius",
            50f,
            "The maximum distance from the Shisha's current position within which it can wander."
        );

        AnchoredWandering = cfg.Bind(
            "General",
            "Anchored Wandering",
            true,
            "When enabled, the Shisha will only wander around its spawn point within a radius defined by the Wander Radius. If disabled, the Shisha can wander from any point within the Wander Radius."
        );

        MaxSpeed = cfg.Bind(
            "General",
            "Max Speed",
            4f,
            "The maximum speed of the Shisha."
        );

        MaxAcceleration = cfg.Bind(
            "General",
            "Max Acceleration",
            5f,
            "The maximum acceleration of the Shisha."
        );

        WanderTimeMin = cfg.Bind(
            "General",
            "Wander Time Minimum",
            5f,
            "The minimum time that the Shisha will wander for."
        );

        WanderTimeMax = cfg.Bind(
            "General",
            "Wander Time Maximum",
            45f,
            "The maximum time that the Shisha will wander for."
        );

        AmbientSfxTimerMin = cfg.Bind(
            "General",
            "Ambient Sfx Time Interval Minimum",
            7.5f,
            "The minimum time gap between any given ambient sound effect."
        );

        AmbientSfxTimerMax = cfg.Bind(
            "General",
            "Ambient Sfx Time Interval Maximum",
            30f,
            "The maximum time gap between any given ambient sound effect."
        );

        TimeInDayLeaveEnabled = cfg.Bind(
            "General",
            "Leave At Night Time Enabled",
            true,
            "Toggles whether the Shisha will leave the map when it gets dark like other vanilla daytime entities."
        );

        PoopBehaviourEnabled = cfg.Bind(
            "General",
            "Poop Behaviour Enabled",
            true,
            "Toggles whether the Shisha can poop when idle."
        );

        PoopChance = cfg.Bind(
            "General",
            "Poop Chance",
            0.05f,
            "The chance from 0 to 1, of the Shisha pooping while idle."
        );

        ShishaEnabled = cfg.Bind(
            "Spawn Values",
            "Shisha Enabled",
            true,
            "Whether the Shisha is enabled (will spawn in games)."
        );

        ShishaSpawnRarity = cfg.Bind(
            "Spawn Values",
            "Shisha Spawn Rarity",
            "All:30",
            "Spawn weight of the Shisha on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config)."
        );

        ShishaMaxAmount = cfg.Bind(
            "Spawn Values",
            "Max Amount",
            4,
            "The max amount of Shisha's that can spawn."
        );

        ShishaPowerLevel = cfg.Bind(
            "Spawn Values",
            "Power Level",
            0.1f,
            "The power level of the Shisha."
        );

        CommonCrystalChance = cfg.Bind(
            "Spawn Values",
            "Common Crystal Spawn Chance",
            65,
            "The percentage chance of the Shisha pooping a common sized crystal. Make sure the values of all the crystals add up to 100."
        );

        UncommonCrystalChance = cfg.Bind(
            "Spawn Values",
            "Uncommon Crystal Spawn Chance",
            25,
            "The percentage chance of the Shisha pooping a uncommon sized crystal. Make sure the values of all the crystals add up to 100."
        );

        RareCrystalChance = cfg.Bind(
            "Spawn Values",
            "Rare Crystal Spawn Chance",
            10,
            "The percentage chance of the Shisha pooping a rare sized crystal. Make sure the values of all the crystals add up to 100."
        );

        CommonCrystalMinValue = cfg.Bind(
            "Spawn Values",
            "Common Crystal Minimum Value",
            20,
            "The minimum value that the common crystal can spawn with."
        );

        CommonCrystalMaxValue = cfg.Bind(
            "Spawn Values",
            "Common Crystal Maximum Value",
            35,
            "The maximum value that the common crystal can spawn with."
        );

        UncommonCrystalMinValue = cfg.Bind(
            "Spawn Values",
            "Uncommon Crystal Minimum Value",
            40,
            "The minimum value that the uncommon crystal can spawn with."
        );

        UncommonCrystalMaxValue = cfg.Bind(
            "Spawn Values",
            "Uncommon Crystal Maximum Value",
            75,
            "The maximum value that the uncommon crystal can spawn with."
        );

        RareCrystalMinValue = cfg.Bind(
            "Spawn Values",
            "Rare Crystal Minimum Value",
            80,
            "The minimum value that the rare crystal can spawn with."
        );

        RareCrystalMaxValue = cfg.Bind(
            "Spawn Values",
            "Rare Crystal Maximum Value",
            100,
            "The maximum value that the rare crystal can spawn with."
        );

        AmbientSoundEffectsVolume = cfg.Bind(
            "Audio",
            "Ambient Sound Effects Volume",
            0.4f,
            "The volume of the ambient sounds of the Shisha from 0 to 1."
        );

        FootstepSoundEffectsVolume = cfg.Bind(
            "Audio",
            "Footstep Sound Effects Volume",
            0.7f,
            "The volume of the footstep sounds of the Shisha from 0 to 1."
        );
    }
}
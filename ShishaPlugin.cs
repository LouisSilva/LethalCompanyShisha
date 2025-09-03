using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyShisha.Util;
using LethalLib.Modules;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;
using System.Diagnostics;
using UnityEngine;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace LethalCompanyShisha;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
[BepInDependency("linkoid-DissonanceLagFix-1.0.0", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mattymatty-AsyncLoggers-1.6.3", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
public class ShishaPlugin : BaseUnityPlugin
{
    public static ShishaPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger { get; private set; }
    internal new static ShishaConfig Config { get; private set; }
    private Harmony _harmony;

    private static EnemyType _shishaEnemyType;

    public static Item ShishaRedPoopItem;
    public static Item ShishaGreenPoopItem;
    public static Item ShishaBluePoopItem;

    private void Awake()
    {
        Stopwatch timer = Stopwatch.StartNew();
        Logger = BepInEx.Logging.Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_NAME}|{MyPluginInfo.PLUGIN_VERSION}");
        Instance = this;

        if (LobbyCompatibilityChecker.Enabled) LobbyCompatibilityChecker.Init();

        Logger.LogDebug("Creating base biodiversity config."); // Can't use LogVerbose here yet because we need the config to tell us whether verbose logging is enabled or not.
        Config = new ShishaConfig(base.Config);

        LogVerbose("Creating Harmony instance...");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        _harmony.PatchAll();


        if (!ShishaConfig.Instance.ShishaEnabled.Value)
        {
            Logger.LogInfo("Shisha is disabled, not loading asset bundle.");
            return;
        }

        Assets.PopulateAssetsFromFile();
        if (!Assets.MainAssetBundle)
        {
            Logger.LogError("MainAssetBundle is null");
            return;
        }

        SetupShisha();

        ShishaRedPoopItem = SetupShishaPoop("Red");
        ShishaGreenPoopItem = SetupShishaPoop("Green");
        ShishaBluePoopItem = SetupShishaPoop("Blue");

        _harmony.PatchAll(typeof(ShishaPlugin));
        _harmony.PatchAll(typeof(ShishaPoopBehaviour));

        NetcodePatcher();

        timer.Stop();
        Logger.LogInfo(
            $"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has setup in {timer.ElapsedMilliseconds}ms.");
    }

    private void SetupShisha()
    {
        _shishaEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("ShishaEnemyType");
        _shishaEnemyType.MaxCount = Mathf.Max(0, ShishaConfig.Instance.ShishaMaxAmount.Value);
        _shishaEnemyType.PowerLevel = Mathf.Max(0, ShishaConfig.Instance.ShishaPowerLevel.Value);
        _shishaEnemyType.normalizedTimeInDayToLeave = ShishaConfig.Instance.TimeInDayLeaveEnabled.Value ? 0.6f : 1f;
        _shishaEnemyType.canDie = ShishaConfig.Instance.Killable.Value;

        TerminalNode shishaTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("ShishaTerminalNode");
        TerminalKeyword shishaTerminalKeyword =
            Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("ShishaTerminalKeyword");

        NetworkPrefabs.RegisterNetworkPrefab(_shishaEnemyType.enemyPrefab);
        Utilities.FixMixerGroups(_shishaEnemyType.enemyPrefab);
        RegisterEnemyWithConfig(ShishaConfig.Instance.ShishaEnabled.Value,
            ShishaConfig.Instance.ShishaSpawnRarity.Value, _shishaEnemyType, shishaTerminalNode, shishaTerminalKeyword);
    }

    private static Item SetupShishaPoop(string colour)
    {
        Item poopItem = Assets.MainAssetBundle.LoadAsset<Item>($"Shisha{colour}PoopItemData");

        switch (colour)
        {
            case "Red":
                poopItem.minValue = ShishaConfig.Instance.CommonCrystalMinValue.Value;
                poopItem.maxValue = ShishaConfig.Instance.CommonCrystalMaxValue.Value;
                break;
            case "Green":
                poopItem.minValue = ShishaConfig.Instance.UncommonCrystalMinValue.Value;
                poopItem.maxValue = ShishaConfig.Instance.UncommonCrystalMaxValue.Value;
                break;
            case "Blue":
                poopItem.minValue = ShishaConfig.Instance.RareCrystalMinValue.Value;
                poopItem.maxValue = ShishaConfig.Instance.RareCrystalMaxValue.Value;
                break;
        }

        NetworkPrefabs.RegisterNetworkPrefab(poopItem.spawnPrefab);
        Utilities.FixMixerGroups(poopItem.spawnPrefab);
        Items.RegisterScrap(poopItem, 0, Levels.LevelTypes.All);

        return poopItem;
    }

    private static void RegisterEnemyWithConfig(bool enemyEnabled, string configMoonRarity, EnemyType enemy,
        TerminalNode terminalNode, TerminalKeyword terminalKeyword)
    {
        if (enemyEnabled)
        {
            (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType,
                Dictionary<string, int> spawnRateByCustomLevelType) = ConfigParsing(configMoonRarity);
            Enemies.RegisterEnemy(enemy, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode,
                terminalKeyword);
        }
        else
        {
            Enemies.RegisterEnemy(enemy, 0, Levels.LevelTypes.All, terminalNode, terminalKeyword);
        }
    }

    private static (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int>
        spawnRateByCustomLevelType) ConfigParsing(string configMoonRarity)
    {
        Dictionary<Levels.LevelTypes, int> spawnRateByLevelType = new();
        Dictionary<string, int> spawnRateByCustomLevelType = new();

        foreach (string entry in configMoonRarity.Split(',').Select(s => s.Trim()))
        {
            string[] entryParts = entry.Split(':');

            if (entryParts.Length != 2) continue;
            string name = entryParts[0];
            if (!int.TryParse(entryParts[1], out int spawnrate)) continue;

            if (Enum.TryParse(name, true, out Levels.LevelTypes levelType))
            {
                spawnRateByLevelType[levelType] = spawnrate;
                LogVerbose($"Registered spawn rate for level type {levelType} to {spawnrate}");
            }
            else
            {
                // Try appending "Level" to the name and re-attempt parsing
                string modifiedName = name + "Level";
                if (Enum.TryParse(modifiedName, true, out levelType))
                {
                    spawnRateByLevelType[levelType] = spawnrate;
                    LogVerbose($"Registered spawn rate for level type {levelType} to {spawnrate}");
                }
                else
                {
                    spawnRateByCustomLevelType[name] = spawnrate;
                    LogVerbose($"Registered spawn rate for custom level type {name} to {spawnrate}");
                }
            }
        }

        return (spawnRateByLevelType, spawnRateByCustomLevelType);
    }

    private static void NetcodePatcher()
    {
        try
        {
            IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetLoadableTypes();
            foreach (Type type in types)
            {
                MethodInfo[] methods =
                    type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                foreach (MethodInfo method in methods)
                {
                    if (!Attribute.IsDefined(method, typeof(RuntimeInitializeOnLoadMethodAttribute)))
                        continue;

                    // Needed because patching the network stuff in the generic StateManagedAI class produces an error
                    if (method.ContainsGenericParameters)
                    {
                        Logger.LogDebug(
                            $"[NetcodePatcher] Skipping generic method {type.FullName}.{method.Name} with [RuntimeInitializeOnLoadMethod] attribute.");
                        continue;
                    }

                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch (Exception invokeException)
                    {
                        Logger.LogError($"Error invoking method {type.FullName}.{method.Name}: {invokeException}");
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException reflectionException)
        {
            Logger.LogError($"[NetcodePatcher] Error loading types from assembly: {reflectionException}");

            for (int i = 0; i < reflectionException.LoaderExceptions.Length; i++)
            {
                Exception loaderException = reflectionException.LoaderExceptions[i];
                if (loaderException != null)
                {
                    Logger.LogError($"[NetcodePatcher] Loader Exception: {loaderException.Message}");
                }
            }
        }
    }

    internal static void LogVerbose(object message)
    {
        if (Config.VerboseLoggingEnabled)
            Logger.LogDebug(message);
    }
}

internal static class Assets
{
    private const string MainAssetBundleName = "shishabundle";
    public static AssetBundle MainAssetBundle;

    public static void PopulateAssetsFromFile()
    {
        if (MainAssetBundle) return;
        string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (assemblyLocation != null)
        {
            MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(assemblyLocation, MainAssetBundleName));

            if (MainAssetBundle) return;
            string assetsPath = Path.Combine(assemblyLocation, "Assets");
            MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(assetsPath, MainAssetBundleName));
        }

        if (!MainAssetBundle)
        {
            ShishaPlugin.Mls.LogWarning($"Failed to load {MainAssetBundleName} bundle");
        }
    }
}

internal static class LobbyCompatibilityChecker
{
    internal static bool Enabled => Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility");

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    internal static void Init()
    {
        PluginHelper.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, Version.Parse(MyPluginInfo.PLUGIN_VERSION),
            CompatibilityLevel.Everyone, VersionStrictness.Patch);
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Features;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace LethalCompanyShisha;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
[BepInDependency("linkoid-DissonanceLagFix-1.0.0", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mattymatty-AsyncLoggers-1.6.3", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mattymatty-Matty_Fixes-1.0.21", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
public class ShishaPlugin : BaseUnityPlugin
{
    public const string ModGuid = $"LCM_Shisha|{ModVersion}";
    private const string ModName = "Lethal Company Shisha Mod";
    private const string ModVersion = "1.0.4";
    
    private readonly Harmony _harmony = new(ModGuid);

    public static readonly ManualLogSource Mls = BepInEx.Logging.Logger.CreateLogSource(ModGuid);
    
    public static ShishaConfig ShishaConfigInstance { get; internal set; }

    private static ShishaPlugin _instance;

    private static EnemyType _shishaEnemyType;

    public static Item ShishaPoopItem;
    
    private void Awake()
    {
        if (_instance == null) _instance = this;
        if (LobbyCompatibilityChecker.Enabled) LobbyCompatibilityChecker.Init();

        InitializeNetworkStuff();
        
        Assets.PopulateAssetsFromFile();
        if (Assets.MainAssetBundle == null)
        {
            Mls.LogError("MainAssetBundle is null");
            return;
        }
        
        _harmony.PatchAll();
        ShishaConfigInstance = new ShishaConfig(Config);
        
        SetupShisha();
        SetupShishaPoop();
        
        _harmony.PatchAll();
        _harmony.PatchAll(typeof(ShishaPlugin));
        Mls.LogInfo($"Plugin {ModName} is loaded!");
    }
    
    private void SetupShisha()
    {
        _shishaEnemyType = Assets.MainAssetBundle.LoadAsset<EnemyType>("ShishaEnemyType");
        _shishaEnemyType.MaxCount = Mathf.Max(0, ShishaConfig.Instance.ShishaMaxAmount.Value);
        
        TerminalNode shishaTerminalNode = Assets.MainAssetBundle.LoadAsset<TerminalNode>("ShishaTerminalNode");
        TerminalKeyword shishaTerminalKeyword = Assets.MainAssetBundle.LoadAsset<TerminalKeyword>("ShishaTerminalKeyword");
        
        NetworkPrefabs.RegisterNetworkPrefab(_shishaEnemyType.enemyPrefab);
        Utilities.FixMixerGroups(_shishaEnemyType.enemyPrefab);
        RegisterEnemyWithConfig(ShishaConfig.Instance.ShishaEnabled.Value, ShishaConfig.Instance.ShishaSpawnRarity.Value, _shishaEnemyType, shishaTerminalNode, shishaTerminalKeyword);
    }

    private void SetupShishaPoop()
    {
        ShishaPoopItem = Assets.MainAssetBundle.LoadAsset<Item>("ShishaPoopItemData");
        
        NetworkPrefabs.RegisterNetworkPrefab(ShishaPoopItem.spawnPrefab);
        Utilities.FixMixerGroups(ShishaPoopItem.spawnPrefab);
        Items.RegisterScrap(ShishaPoopItem, 0, Levels.LevelTypes.All);
    }
    
    private void RegisterEnemyWithConfig(bool enemyEnabled, string configMoonRarity, EnemyType enemy, TerminalNode terminalNode, TerminalKeyword terminalKeyword) {
        if (enemyEnabled) { 
            (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) = ConfigParsing(configMoonRarity);
            Enemies.RegisterEnemy(enemy, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode, terminalKeyword);
                
        } else {
            Enemies.RegisterEnemy(enemy, 0, Levels.LevelTypes.All, terminalNode, terminalKeyword);
        }
    }
    
    private static (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) ConfigParsing(string configMoonRarity) {
        Dictionary<Levels.LevelTypes, int> spawnRateByLevelType = new();
        Dictionary<string, int> spawnRateByCustomLevelType = new();
        foreach (string entry in configMoonRarity.Split(',').Select(s => s.Trim())) {
            string[] entryParts = entry.Split(':');

            if (entryParts.Length != 2) continue;
            string name = entryParts[0];
            if (!int.TryParse(entryParts[1], out int spawnrate)) continue;

            if (Enum.TryParse(name, true, out Levels.LevelTypes levelType)) {
                spawnRateByLevelType[levelType] = spawnrate;
                Mls.LogDebug($"Registered spawn rate for level type {levelType} to {spawnrate}");
            } else {
                spawnRateByCustomLevelType[name] = spawnrate;
                Mls.LogDebug($"Registered spawn rate for custom level type {name} to {spawnrate}");
            }
        }
        return (spawnRateByLevelType, spawnRateByCustomLevelType);
    }
    
    private static void InitializeNetworkStuff()
    {
        IEnumerable<Type> types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null);
        }
        
        foreach (Type type in types)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}

internal static class Assets
{
    private const string MainAssetBundleName = "shishabundle";
    public static AssetBundle MainAssetBundle;

    public static void PopulateAssetsFromFile()
    {
        if (MainAssetBundle != null) return;
        string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation != null)
        {
            MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(assemblyLocation, MainAssetBundleName));

            if (MainAssetBundle != null) return;
            string assetsPath = Path.Combine(assemblyLocation, "Assets");
            MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(assetsPath, MainAssetBundleName));
        }

        if (MainAssetBundle == null)
        {
            ShishaPlugin.Mls.LogWarning($"Failed to load {MainAssetBundleName} bundle");
        }
    }
}

[Serializable]
public class SyncedInstance<T>
{
    internal static CustomMessagingManager MessageManager => NetworkManager.Singleton.CustomMessagingManager;
    internal static bool IsClient => NetworkManager.Singleton.IsClient;
    internal static bool IsHost => NetworkManager.Singleton.IsHost;
        
    [NonSerialized]
    protected static int IntSize = 4;

    public static T Default { get; private set; }
    public static T Instance { get; private set; }

    public static bool Synced { get; internal set; }

    protected void InitInstance(T instance) {
        Default = instance;
        Instance = instance;
            
        IntSize = sizeof(int);
    }

    internal static void SyncInstance(byte[] data) {
        Instance = DeserializeFromBytes(data);
        Synced = true;
    }

    internal static void RevertSync() {
        Instance = Default;
        Synced = false;
    }

    public static byte[] SerializeToBytes(T val) {
        BinaryFormatter bf = new();
        using MemoryStream stream = new();

        try {
            bf.Serialize(stream, val);
            return stream.ToArray();
        }
        catch (Exception e) {
            Debug.LogError($"Error serializing instance: {e}");
            return null;
        }
    }

    public static T DeserializeFromBytes(byte[] data) {
        BinaryFormatter bf = new();
        using MemoryStream stream = new(data);

        try {
            return (T) bf.Deserialize(stream);
        } catch (Exception e) {
            Debug.LogError($"Error deserializing instance: {e}");
            return default;
        }
    }
        
    private static void RequestSync() {
        if (!IsClient) return;

        using FastBufferWriter stream = new(IntSize, Allocator.Temp);
        MessageManager.SendNamedMessage($"{ShishaPlugin.ModGuid}_OnRequestConfigSync", 0uL, stream);
    }

    private static void OnRequestSync(ulong clientId, FastBufferReader _) {
        if (!IsHost) return;

        Debug.Log($"Config sync request received from client: {clientId}");

        byte[] array = SerializeToBytes(Instance);
        int value = array.Length;

        using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

        try {
            stream.WriteValueSafe(in value);
            stream.WriteBytesSafe(array);

            MessageManager.SendNamedMessage($"{ShishaPlugin.ModGuid}_OnReceiveConfigSync", clientId, stream);
        } catch(Exception e) {
            Debug.Log($"Error occurred syncing config with client: {clientId}\n{e}");
        }
    }

    private static void OnReceiveSync(ulong _, FastBufferReader reader) {
        if (!reader.TryBeginRead(IntSize)) {
            Debug.LogError("Config sync error: Could not begin reading buffer.");
            return;
        }

        reader.ReadValueSafe(out int val);
        if (!reader.TryBeginRead(val)) {
            Debug.LogError("Config sync error: Host could not sync.");
            return;
        }

        byte[] data = new byte[val];
        reader.ReadBytesSafe(ref data, val);

        SyncInstance(data);

        Debug.Log("Successfully synced config with host.");
    }
        
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
    public static void InitializeLocalPlayer() {
        if (IsHost) {
            MessageManager.RegisterNamedMessageHandler($"{ShishaPlugin.ModGuid}_OnRequestConfigSync", OnRequestSync);
            Synced = true;

            return;
        }

        Synced = false;
        MessageManager.RegisterNamedMessageHandler($"{ShishaPlugin.ModGuid}_OnReceiveConfigSync", OnReceiveSync);
        RequestSync();
    }
        
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
    public static void PlayerLeave() {
        RevertSync();
    }
}

public static class LobbyCompatibilityChecker 
{
    public static bool Enabled => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility");

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void Init() {
        PluginHelper.RegisterPlugin(PluginInfo.PLUGIN_GUID, Version.Parse(PluginInfo.PLUGIN_VERSION), CompatibilityLevel.Everyone, VersionStrictness.Patch);
    }
}
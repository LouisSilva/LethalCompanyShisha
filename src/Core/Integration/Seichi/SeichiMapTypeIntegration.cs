using LethalCompanyShisha.Enemies;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalCompanyShisha.Core.Integration.Seichi;

public class SeichiMapTypeIntegration : MonoBehaviour
{
    private readonly HashSet<ShishaServer> _trackedShishas = [];
    private readonly HashSet<SeichiMapTypeNotifier> _activeMapTypes = [];

    private SeichiMapTypeNotifier dominantMapType;

    private static readonly object _padlock = new();
    private static SeichiMapTypeIntegration _instance;
    public static SeichiMapTypeIntegration Instance
    {
        get
        {
            if (!IsOnCorrectScene()) return null;

            if (!_instance)
            {
                _instance = FindAnyObjectByType<SeichiMapTypeIntegration>();

                lock (_padlock)
                {
                    if (!_instance)
                    {
                        GameObject singleton = new() {name = "SeichiMapTypeIntegration"};
                        _instance = singleton.AddComponent<SeichiMapTypeIntegration>();
                        _instance.Setup();
                        ShishaPlugin.LogVerbose("[SeichiMapTypeIntegration] Created a new SeichiMapTypeIntegration object.");
                    }
                }
            }

            return _instance;
        }
    }

    private void OnDestroy()
    {
        _instance = null;
    }

    public void RegisterShisha(ShishaServer shisha)
    {
        _trackedShishas.Add(shisha);
        UpdateShishaSkin(shisha);
    }

    public void UnregisterShisha(ShishaServer shisha)
    {
        _trackedShishas.Remove(shisha);
        UpdateShishaSkin(shisha);
    }

    public void RegisterMapTypeNotifier(SeichiMapTypeNotifier notifier)
    {
        _activeMapTypes.Add(notifier);
        RecalculateDominantWeather();
    }

    public void UnregisterMapTypeNotifier(SeichiMapTypeNotifier notifier)
    {
        _activeMapTypes.Remove(notifier);
        RecalculateDominantWeather();
    }

    private void RecalculateDominantWeather()
    {
        // If the dominant map type is null, then it just uses the default skin
        SeichiMapTypeNotifier newDominantMapType = null;
        int currentImportance = int.MinValue;

        foreach (SeichiMapTypeNotifier n in _activeMapTypes)
        {
            int importance = (int)n.mapType;
            if (importance > currentImportance)
            {
                currentImportance = importance;
                newDominantMapType = n;
            }
        }

        if (newDominantMapType != dominantMapType)
        {
            dominantMapType = newDominantMapType;
            UpdateAllShishaSkins();
        }
    }

    private void UpdateShishaSkin(ShishaServer shisha)
    {
        if (!NetworkManager.Singleton.IsServer || !shisha) return;

        shisha.Context.Blackboard.NetcodeController.SetSkinTypeServerRpc(dominantMapType ? dominantMapType.skinType : ShishaSkinType.Default);
    }

    private void UpdateAllShishaSkins()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        foreach (ShishaServer shisha in _trackedShishas)
        {
            UpdateShishaSkin(shisha);
        }
    }

    public void Setup()
    {
        GameObject[] rootObjects = SceneManager.GetSceneByName("Seichi").GetRootGameObjects();
        Transform environmentTransform = null;

        foreach (GameObject rootObject in rootObjects)
        {
            if (rootObject.name == "Environment")
            {
                environmentTransform = rootObject.transform;
                break;
            }
        }

        if (!environmentTransform)
        {
            ShishaPlugin.LogVerbose("[SeichiMapTypeIntegration] Couldn't find the Environment transform.");
            return;
        }

        SetupWeatherNotifier(environmentTransform, "v0xxManager/Snowichi", SeichiMapType.Snowichi, ShishaSkinType.Snow);
        SetupWeatherNotifier(environmentTransform, "WebleyManager/Scorchi", SeichiMapType.Scorchi, ShishaSkinType.Hell);
        SetupWeatherNotifier(environmentTransform, "Halloween/Spookichi", SeichiMapType.Spookichi, ShishaSkinType.Spooky);
    }

    private void SetupWeatherNotifier(Transform environment, string path, SeichiMapType type, ShishaSkinType skin)
    {
        Transform notifierObjectTransform = environment.Find(path);
        if (notifierObjectTransform && !notifierObjectTransform.gameObject.TryGetComponent<SeichiMapTypeNotifier>(out _))
        {
            SeichiMapTypeNotifier notifierComponent = notifierObjectTransform.gameObject.AddComponent<SeichiMapTypeNotifier>();
            notifierComponent.mapType = type;
            notifierComponent.skinType = skin;
        }
    }

    private static bool IsOnCorrectScene()
    {
        Scene targetScene = SceneManager.GetSceneByName("Seichi");
        return targetScene.IsValid() && targetScene.isLoaded;
    }
}
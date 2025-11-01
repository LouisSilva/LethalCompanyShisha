using LethalCompanyShisha.Enemies;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalCompanyShisha.Core.Integration;

// Seichi "alternative map" integration
public class SeichiAltMapIntegration : MonoBehaviour
{
    public string targetSceneName = "Seichi";
    public ShishaClient.SkinType skinTypeWhenEnabled = ShishaClient.SkinType.Default;

    private readonly HashSet<ShishaServer> trackedShishas = [];

    private void OnEnable()
    {
        if (!NetworkManager.Singleton.IsServer || !IsOnCorrectScene()) return;

        foreach (ShishaServer shisha in trackedShishas)
        {
            if (!shisha)
            {
                RemoveTrackedShisha(shisha);
                continue;
            }

            shisha.Context.Blackboard.NetcodeController.SetSkinTypeClientRpc(skinTypeWhenEnabled);
        }
    }

    private void OnDisable()
    {
        if (!NetworkManager.Singleton.IsServer || !IsOnCorrectScene()) return;

        foreach (ShishaServer shisha in trackedShishas)
        {
            if (!shisha)
            {
                RemoveTrackedShisha(shisha);
                continue;
            }

            shisha.Context.Blackboard.NetcodeController.SetSkinTypeClientRpc(ShishaClient.SkinType.Default);
        }
    }

    private bool IsOnCorrectScene()
    {
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        return targetScene.IsValid() && targetScene.isLoaded;
    }

    public void AddTrackedShisha(ShishaServer shisha)
    {
        trackedShishas.Add(shisha);
    }

    public void RemoveTrackedShisha(ShishaServer shisha)
    {
        trackedShishas.Remove(shisha);
    }
}
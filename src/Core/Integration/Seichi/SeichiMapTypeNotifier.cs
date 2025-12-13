using LethalCompanyShisha.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace LethalCompanyShisha.Core.Integration.Seichi;

public class SeichiMapTypeNotifier : MonoBehaviour
{
    public SeichiMapType mapType;
    public ShishaSkinType skinType;

    private void OnEnable()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        SeichiMapTypeIntegration.Instance?.RegisterMapTypeNotifier(this);
    }

    private void OnDisable()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        SeichiMapTypeIntegration.Instance?.UnregisterMapTypeNotifier(this);
    }
}
using LethalCompanyShisha.Core.AI;
using LethalCompanyShisha.Util;
using UnityEngine;

namespace LethalCompanyShisha.Enemies;

public class ShishaBlackboard : IEnemyBlackboard
{
    public bool IsKillable { get; set; }
    public bool IsWanderEnabled { get; set; }
    public bool IsAnchoredWanderEnabled { get; set; }
    public bool IsPoopBehaviourEnabled { get; set; }
    public bool IsTimeInDayLeaveEnabled { get; set; }

    public double WanderCycleEndTime { get; set; }

    public float PoopProbability { get; set; } = 0.05f;

    public WeightedPicker<ShishaPoopBehaviour.PoopType> PoopPicker;

    // public Vector2 WanderTimeRange; // new(5f, 45f)
    // public Vector2 AmbientSfxTimerRange; // new(7.5f, 40f);

    public Vector3 SpawnPosition { get; set; }

    public Transform RunAwayTransform { get; set; }
    public Transform PoopPlaceholder { get; set; }

    public ShishaNetcodeController NetcodeController { get; set; }
}
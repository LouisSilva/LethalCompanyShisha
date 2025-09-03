using LethalCompanyShisha.Core;
using UnityEngine;

namespace LethalCompanyShisha;

public class ShishaBlackboard : IEnemyBlackboard
{
    public bool IsKillable { get; set; }
    public bool IsWanderEnabled { get; set; }
    public bool IsAnchoredWanderEnabled { get; set; }
    public bool IsPoopBehaviourEnabled { get; set; }
    public bool IsTimeInDayLeaveEnabled { get; set; }

    public float WanderTimer { get; set; }
    public float PoopProbability { get; set; } = 0.05f;

    public Vector2 WanderTimeRange; // new(5f, 45f)
    public Vector2 AmbientSfxTimerRange; // new(7.5f, 40f);

    public Vector3 SpawnPosition { get; set; }
    public Vector3 IdlePosition { get; set; }

    public Transform RunAwayTransform { get; set; }
}
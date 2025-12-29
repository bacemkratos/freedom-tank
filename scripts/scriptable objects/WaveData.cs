using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Level/Wave")]
public class WaveData : ScriptableObject
{
    [Header("Wave Spawn")]
    [Tooltip("Delay before this wave starts spawning (seconds).")]
    public float startDelay = 0f;

    [Tooltip("Adds random delay in range [-startDelayJitter, +startDelayJitter].")]
    public float startDelayJitter = 0f;

    public List<EnemySpawnData> enemies;
}

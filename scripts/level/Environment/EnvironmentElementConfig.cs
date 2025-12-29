using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnvironmentSpawnMode
{
    Tiled,      // road-like continuous tiling
    Discrete    // individual objects, spaced with random gaps
}

[Serializable]
public class EnvironmentElementConfig
{
    [Header("Identification")]
    public string id = "road";       // e.g. "road", "left_buildings", "lamps"

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Spawn Mode")]
    public EnvironmentSpawnMode spawnMode = EnvironmentSpawnMode.Tiled;

    [Header("Movement")]
    [Tooltip("Scroll speed = baseScrollSpeed * scrollSpeedMultiplier.")]
    public float scrollSpeedMultiplier = 1f;

    [Header("Placement")]
    [Tooltip("Base position (x,y,z offset) for this element. Z offset adds to tiling/spawn position.")]
    public Vector3 basePosition = Vector3.zero;

    // ---------- TILED SETTINGS (road-like) ----------

    [Header("Tiled Settings (for SpawnMode = Tiled)")]
    [Tooltip("If 0, will be auto-calculated from prefab bounds along Z.")]
    public float segmentLength = 0f;

    [Tooltip("How far ahead of the reference point this element should cover.")]
    public float viewAheadDistance = 60f;

    [Tooltip("Extra segments to keep as buffer beyond viewAheadDistance.")]
    public int extraSegments = 2;

    [Tooltip("How far behind the reference point before a segment is recycled.")]
    public float despawnDistanceBehind = 20f;

    // ---------- DISCRETE SETTINGS (lamps, buildings, props) ----------

    [Header("Discrete Settings (for SpawnMode = Discrete)")]
    [Tooltip("How many instances to manage at the same time.")]
    public int initialInstances = 6;

    [Tooltip("Minimum gap (in world units along Z) between consecutive instances.")]
    public float minGapZ = 10f;

    [Tooltip("Maximum gap (in world units along Z) between consecutive instances.")]
    public float maxGapZ = 25f;

    [Tooltip("Random horizontal offset from basePosition.x (e.g. -2..2).")]
    public float randomOffsetX = 0f;

    [Tooltip("Random vertical offset from basePosition.y (e.g. -1..1).")]
    public float randomOffsetY = 0f;
}

[Serializable]
public class EnvironmentElementRuntime
{
    public EnvironmentElementConfig config;
    public List<Transform> instances = new List<Transform>();
    public bool initialized;
}

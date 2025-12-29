using UnityEngine;

public enum SpawnSide { Left, Right, Random }
public enum SpawnMode { Offscreen, FixedWorld }

[System.Serializable]
public class EnemySpawnData
{
    public GameObject prefab;
    public int count = 1;
    public float spawnInterval = 0.5f;

    [Header("Side / Mode")]
    public SpawnSide side = SpawnSide.Random;
    public SpawnMode mode = SpawnMode.Offscreen;

    [Header("Offscreen Spawn Settings")]
    public float offscreenMarginX = 2f;          // extra world margin outside camera
    [Range(0f, 1f)] public float minYViewport = 0.55f; // bottom bound in viewport (0..1)
    [Range(0f, 1f)] public float maxYViewport = 0.95f; // top bound in viewport (0..1)
    public float minAbovePlayerY = 0f;           // extra margin above player.y
    public float maxBelowTopWorld = 0f;          // margin below top of screen in world units
    public float zOffset = 0f;                   // enemy-specific Z offset relative to player
    public float zRandomRange = 0f;              // random +/- range around zOffset

    [Header("Fixed World Spawn Settings")]
    public Vector3 fixedWorldPosition;           // used if mode == FixedWorld
    public bool fixedUsePlayerZ = true;          // keep same Z plane as player (plus offsets)
}

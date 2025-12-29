using UnityEngine;

public interface IEnemySpawnable
{
    void OnSpawned(EnemySpawnContext ctx);
}

public struct EnemySpawnContext
{
    public Transform player;
    public Camera cam;
    public Vector3 spawnPos;
    public SpawnSide side;     // your global SpawnSide enum
}

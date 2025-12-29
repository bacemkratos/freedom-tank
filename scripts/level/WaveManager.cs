using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    [SerializeField] private List<WaveData> waves;

    private int currentWaveIndex = 0;
    private int aliveEnemies = 0;

    private Coroutine _runRoutine;
    private bool _stopped;

    private Transform player;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    public void StartWaves()
    {
        StopWaves();
        currentWaveIndex = 0;
        aliveEnemies = 0;

        _stopped = false;
        _runRoutine = StartCoroutine(RunWavesLoop());
    }

    public void StopWaves()
    {
        _stopped = true;

        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }
    }

    private IEnumerator RunWavesLoop()
    {
        while (!_stopped)
        {
            if (currentWaveIndex >= waves.Count)
            {
                if (aliveEnemies <= 0)
                {
                    EventBus.Raise(new AllWavesCompletedEvent());
                    yield break;
                }

                yield return null;
                continue;
            }

            var wave = waves[currentWaveIndex];

            // ✅ NEW: wait before starting this wave
            float delay = Mathf.Max(0f, wave.startDelay);
            if (wave.startDelayJitter > 0f)
                delay += Random.Range(-wave.startDelayJitter, wave.startDelayJitter);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            // Spawn this wave
            yield return StartCoroutine(SpawnCurrentWave());

            // Wait until all enemies from the wave are destroyed
            while (!_stopped && aliveEnemies > 0)
                yield return null;

            if (_stopped) yield break;

            currentWaveIndex++;
        }
    }


    private IEnumerator SpawnCurrentWave()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (cam == null) cam = Camera.main;

        var wave = waves[currentWaveIndex];

        // Start one coroutine per enemy type, and track completion
        int pending = wave.enemies.Count;

        foreach (var enemy in wave.enemies)
        {
            StartCoroutine(SpawnEnemyType(enemy, () => pending--));
        }

        // Wait until ALL spawn routines have finished
        while (!_stopped && pending > 0)
            yield return null;
    }

    private IEnumerator SpawnEnemyType(EnemySpawnData data, System.Action onDone = null)
    {
        // Ensure pending always decrements even if stopped mid-way
        try
        {
            for (int i = 0; i < data.count; i++)
            {
                if (_stopped) yield break;

                SpawnEnemy(data);

                if (data.spawnInterval > 0f)
                    yield return new WaitForSeconds(data.spawnInterval);
                else
                    yield return null;
            }
        }
        finally
        {
            onDone?.Invoke();
        }
    }

    private void SpawnEnemy(EnemySpawnData data)
    {
        if (player == null || cam == null || data.prefab == null)
            return;

        SpawnSide resolvedSide = data.side;
        if (resolvedSide == SpawnSide.Random)
            resolvedSide = (Random.value < 0.5f) ? SpawnSide.Left : SpawnSide.Right;

        Vector3 spawnPos = EnemySpawnResolver.ResolveSpawn(data, cam, player, resolvedSide);

        GameObject go = Instantiate(data.prefab, spawnPos, Quaternion.identity);
        aliveEnemies++;

        // Any enemy can receive spawn context
        var spawnable = go.GetComponent<IEnemySpawnable>();
        if (spawnable != null)
        {
            spawnable.OnSpawned(new EnemySpawnContext
            {
                player = player,
                cam = cam,
                spawnPos = spawnPos,
                side = resolvedSide
            });
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnemyDestroyedEvent>(OnEnemyDestroyed);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnemyDestroyedEvent>(OnEnemyDestroyed);
    }

    private void OnEnemyDestroyed(EnemyDestroyedEvent evt)
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
    }
}

using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Auto End (optional)")]
    [SerializeField] private float autoEndAfterSeconds = -1f;

    [Header("Wave System (optional)")]
    [Tooltip("If assigned, the level will start waves on LevelStart, and can end when waves finish.")]
    [SerializeField] private WaveManager waveManager;

    [Tooltip("If true, LevelManager will call EndLevel() automatically when all waves are completed.")]
    [SerializeField] private bool endLevelWhenWavesComplete = true;

    private bool _levelRunning;

    private void Start()
    {
        StartLevel();
    }

    private void OnEnable()
    {
        // If WaveManager exists, we can listen to "all waves done"
        EventBus.Subscribe<AllWavesCompletedEvent>(OnAllWavesCompleted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<AllWavesCompletedEvent>(OnAllWavesCompleted);
    }

    private void StartLevel()
    {
        EventBus.Raise(new EnvironmentSpeedChangedEvent(10f));
        EventBus.Raise(new TankSpeedChangedEvent(0f));
        if (_levelRunning) return;
        _levelRunning = true;

        Debug.Log("[LevelManager] Level started.");
        EventBus.Raise(new LevelStartEvent());

        // ✅ Start waves without changing your existing flow
        if (waveManager != null)
        {
            Debug.Log("[LevelManager] Calling StartWaves()");
            waveManager.StartWaves();
        }

        // ✅ Keep your existing auto-end behavior intact
        if (autoEndAfterSeconds > 0f)
        {
            Invoke(nameof(EndLevel), autoEndAfterSeconds);
        }
    }

    private void OnAllWavesCompleted(AllWavesCompletedEvent evt)
    {
        if (!_levelRunning) return;

        Debug.Log("[LevelManager] All waves completed.");

        if (endLevelWhenWavesComplete)
        {
            EndLevel();
        }
    }

    public void EndLevel()
    {
        if (!_levelRunning) return;
        _levelRunning = false;

        // ✅ Prevent Invoke from ending again later
        CancelInvoke(nameof(EndLevel));

        // ✅ Optional: stop waves spawning if level ends early
        if (waveManager != null)
        {
            waveManager.StopWaves();
        }

        Debug.Log("[LevelManager] Level ended.");
        EventBus.Raise(new LevelEndEvent());
    }
}

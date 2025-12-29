using UnityEngine;

public class BackdropScroller : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Very small value. Example: 0.1 or 0.05")]
    [SerializeField] private float scrollSpeed = 0.1f;

    [Tooltip("If true, moves left. If false, moves right.")]
    [SerializeField] private bool moveLeft = true;

    [Header("Run Control")]
    [SerializeField] private bool startRunning = true;

    [Header("Event Mapping")]
    [Tooltip("EnvironmentSpeedChangedEvent speed (ex: 10) * this multiplier => backdrop scrollSpeed (ex: 0.1).")]
    [SerializeField] private float eventSpeedMultiplier = 0.01f;

    private bool _isRunning;

    private void Awake()
    {
        _isRunning = startRunning;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);
    }

    private void OnEnvSpeedChanged(EnvironmentSpeedChangedEvent evt)
    {
        _isRunning = evt.speed > 0.0001f;

        // Keep your "tiny speed" style for backdrops:
        scrollSpeed = Mathf.Abs(evt.speed) * eventSpeedMultiplier;
    }

    private void Update()
    {
        if (!_isRunning) return;

        float dir = moveLeft ? -1f : 1f;
        transform.Translate(Vector3.right * dir * scrollSpeed * Time.deltaTime, Space.World);
    }

    // ---- Optional public controls (same pattern as your other scripts) ----
    public void StartScroll() => _isRunning = true;
    public void StopScroll() => _isRunning = false;

    public void SetSpeed(float speed) => scrollSpeed = speed;
    public void SetDirection(bool toLeft) => moveLeft = toLeft;
}

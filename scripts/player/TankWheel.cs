using UnityEngine;

public class TankWheelRotation : MonoBehaviour
{
    [Header("Direction Fix")]
    [SerializeField] private bool invertRotation = false;

    [Header("Wheel Settings")]
    [SerializeField] private float wheelRotationMultiplier = 50f;

    [Tooltip("Local axis around which the wheel rotates.")]
    [SerializeField] private Vector3 localRotationAxis = Vector3.up;

    [Header("Run Gate")]
    [Tooltip("If true, wheel rotates only after LevelStartEvent.")]
    [SerializeField] private bool requireLevelStart = false;

    private float _environmentSpeed = 0f;
    private float _tankSpeed = 0f;
    private bool _levelRunning = false;

    private void OnEnable()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelEndEvent>(OnLevelEnd);
        EventBus.Subscribe<EnvironmentSpeedChangedEvent>(OnEnvironmentSpeedChanged);
        EventBus.Subscribe<TankSpeedChangedEvent>(OnTankSpeedChanged);

        // ✅ for testing (or if you don't use LevelStart yet)
        if (!requireLevelStart)
            _levelRunning = true;
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelEndEvent>(OnLevelEnd);
        EventBus.Unsubscribe<EnvironmentSpeedChangedEvent>(OnEnvironmentSpeedChanged);
        EventBus.Unsubscribe<TankSpeedChangedEvent>(OnTankSpeedChanged);
    }

    private void Update()
    {
        if (requireLevelStart && !_levelRunning)
            return;

        float combinedSpeed = _environmentSpeed + _tankSpeed;
        if (Mathf.Abs(combinedSpeed) <= 0.0001f)
            return;

        float sign = invertRotation ? -1f : 1f;
        float finalSpeed = combinedSpeed * wheelRotationMultiplier * sign;

        transform.Rotate(localRotationAxis.normalized, finalSpeed * Time.deltaTime, Space.Self);
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        _levelRunning = true;

        // ✅ DO NOT reset speeds here (or you'll cancel current env movement)
        // _environmentSpeed = 0f;
        // _tankSpeed = 0f;
    }

    private void OnLevelEnd(LevelEndEvent e)
    {
        _levelRunning = false;
        _environmentSpeed = 0f;
        _tankSpeed = 0f;
    }

    private void OnEnvironmentSpeedChanged(EnvironmentSpeedChangedEvent e)
    {
        _environmentSpeed = e.speed;
    }

    private void OnTankSpeedChanged(TankSpeedChangedEvent e)
    {
        _tankSpeed = e.speed;
    }
}

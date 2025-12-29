using UnityEngine;

public class EnvironmentSpeedToggleTester : MonoBehaviour
{
    [Header("Test Values")]
    public float runningSpeed = 10f;
    public float stoppedSpeed = 0f;

    [Header("Initial State")]
    public bool startRunning = true;   // ✅ IMPORTANT

    private bool isRunning;

    private void Start()
    {
        isRunning = startRunning;

        // ✅ Broadcast initial state so wheel + all listeners are synced
        EventBus.Raise(new EnvironmentSpeedChangedEvent(isRunning ? runningSpeed : stoppedSpeed));
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isRunning = !isRunning;
            float newSpeed = isRunning ? runningSpeed : stoppedSpeed;

            Debug.Log($"[TEST] Env speed => {newSpeed}");
            EventBus.Raise(new EnvironmentSpeedChangedEvent(newSpeed));
        }
    }
}

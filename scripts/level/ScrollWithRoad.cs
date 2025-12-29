using UnityEngine;

public class ScrollWithRoad : MonoBehaviour
{
    [Header("Scroll Movement")]
    [Tooltip("World direction the environment moves. Example: +X means everything moves right.")]
    public Vector3 scrollDirection = Vector3.right;

    [Tooltip("Initial scroll speed (units/sec). Will be overridden by EnvironmentSpeedChangedEvent.")]
    public float scrollSpeed = 10f;

    [Tooltip("If true, object won't move when paused events are received.")]
    public bool respondToPause = true;

    [Header("Auto Destroy")]
    [Tooltip("If <= 0, won't auto destroy.")]
    public float lifeTime = 1.6f;

    private bool _paused;

    private void OnEnable()
    {
        EventBus.Subscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);

        if (lifeTime > 0f)
            Invoke(nameof(DestroySelf), lifeTime);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);
        CancelInvoke();
    }

    private void OnEnvSpeedChanged(EnvironmentSpeedChangedEvent evt)
    {
        if (!respondToPause) return;

        _paused = evt.speed <= 0.0001f;
        scrollSpeed = Mathf.Abs(evt.speed);
    }

    private void Update()
    {
        if (respondToPause && _paused) return;

        transform.position += scrollDirection.normalized * (scrollSpeed * Time.deltaTime);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}

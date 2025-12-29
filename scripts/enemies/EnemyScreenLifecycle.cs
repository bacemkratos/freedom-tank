using UnityEngine;

[DisallowMultipleComponent]
public class EnemyScreenLifecycle : MonoBehaviour
{
    [Header("Spawn / Despawn")]
    [SerializeField] private float enterGraceSeconds = 1.0f;

    private float spawnTime;
    private bool hasEnteredScreen;
    private bool initialized;

    private Camera cam;

    public bool DisableDespawn { get; set; }
    public bool HasEnteredScreen => hasEnteredScreen;

    public void Init(Camera camera)
    {
        cam = camera != null ? camera : Camera.main;
        spawnTime = Time.time;
        hasEnteredScreen = false;
        initialized = true;
    }

    public void Tick()
    {
        if (!initialized) Init(Camera.main);
        if (DisableDespawn) return;
        if (cam == null) return;

        Vector3 v = cam.WorldToViewportPoint(transform.position);

        bool inside = (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f);
        if (inside) hasEnteredScreen = true;

        // grace period if it spawned already on-screen (rare but possible)
        if (!hasEnteredScreen && (Time.time - spawnTime) < enterGraceSeconds)
            return;

        if (!hasEnteredScreen)
            return;

        bool isOffscreen = v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f;
        if (isOffscreen)
            Destroy(gameObject);
    }
}

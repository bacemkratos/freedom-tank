using UnityEngine;

public class TankShellProjectile : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public string playerTag = "Player";

    [Header("Gameplay")]
    public int damage = 1;

    [Header("Impact VFX (optional)")]
    public GameObject impactVfxPrefab;
    public float impactVfxLifetime = 2f;

    [Header("Lifetime")]
    public float lifeTime = 6f;

    // -------- NEW: ignore shooter (the tank that fired) --------
    private Transform shooterRoot;

    private int environmentLayer = -1;

    private Vector3 velocity;
    private float gravity;
    private float deathAt;

    /// <summary>
    /// Optional: call this right after Instantiate so the shell doesn't hit its own tank.
    /// Example:
    ///   shell.SetShooter(gameObject);
    /// </summary>
    public void SetShooter(GameObject shooter)
    {
        shooterRoot = shooter != null ? shooter.transform : null;
    }

    public void InitManual(Transform target, Vector3 initialVelocity, float gravity, string environmentLayerName)
    {
        this.player = target;
        this.velocity = initialVelocity;
        this.gravity = gravity;

        environmentLayer = LayerMask.NameToLayer(environmentLayerName);
        if (environmentLayer == -1)
            Debug.LogError($"TankShellProjectile: Layer '{environmentLayerName}' not found.");

        deathAt = Time.time + lifeTime;
    }

    private void Update()
    {
        if (Time.time >= deathAt)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;

        // ballistic update (manual)
        velocity.y -= gravity * dt;

        Vector3 currentPos = transform.position;
        Vector3 nextPos = currentPos + velocity * dt;
        nextPos.z = currentPos.z; // keep in gameplay plane

        // raycast between currentPos and nextPos to prevent tunneling
        Vector3 delta = nextPos - currentPos;
        float dist = delta.magnitude;

        if (dist > 0.0001f)
        {
            Vector3 dir = delta / dist;

            if (Physics.Raycast(currentPos, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Collide))
            {
                // -------- NEW: ignore hitting the shooter tank --------
                if (shooterRoot != null && hit.collider != null && hit.collider.transform.IsChildOf(shooterRoot))
                {
                    // do nothing, let the shell continue
                }
                else
                {
                    HandleHit(hit.collider, hit.point);
                    return;
                }
            }
        }

        transform.position = nextPos;
    }

    private void HandleHit(Collider other, Vector3 hitPoint)
    {
        if (other == null) return;

        if (other.CompareTag(playerTag))
        {
            EventBus.Raise(new PlayerHitEvent(damage, hitPoint));
            SpawnImpact(hitPoint);
            Destroy(gameObject);
            return;
        }

        if (environmentLayer != -1 && other.gameObject.layer == environmentLayer)
        {
            SpawnImpact(hitPoint);
            Destroy(gameObject);
            return;
        }

        // Optional: if you want it to hit enemies too, add logic here.
    }

    private void SpawnImpact(Vector3 worldPos)
    {
        if (!impactVfxPrefab) return;

        GameObject fx = Instantiate(impactVfxPrefab, worldPos, Quaternion.identity);
        if (impactVfxLifetime > 0f) Destroy(fx, impactVfxLifetime);
    }
}

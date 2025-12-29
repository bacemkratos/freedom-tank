using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyBulletProjectile : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Env Impact")]
    public int envEffectId = 0;

    [Header("Movement")]
    [SerializeField] private float speed = 60f;
    public bool lockDirectionOnSpawn = true;

    [Header("Rotation")]
    public float rotationOffset = 0f;

    [Header("Gameplay")]
    public int damage = 1;
    public string playerTag = "Player";

    [Header("Environment")]
    [SerializeField] private string environmentLayerName = "env";
    private int environmentLayer;

    [Header("Impact VFX (optional)")]
    public GameObject impactVfxPrefab;
    public float impactVfxLifetime = 2f;

    [Header("Lifetime")]
    public float lifeTime = 3f;

    [Header("2.5D")]
    public bool keepBulletZPlane = true;

    private Vector3 moveDir = Vector3.right;
    private float deathTime;

    private Vector3 lockedTargetPos;
    private bool hasLockedTargetPos = false;

    private bool isDead = false;
    private Rigidbody rb;

    private void Awake()
    {
        environmentLayer = LayerMask.NameToLayer(environmentLayerName);
        if (environmentLayer == -1)
            Debug.LogError($"EnemyBulletProjectile: Layer '{environmentLayerName}' not found.");

        deathTime = Time.time + Mathf.Max(0.05f, lifeTime);

        // ✅ Required for trigger callbacks to be reliable
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // strongly recommended: bullet collider should be trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) player = p.transform;
        }

        InitDirection();
    }

    public void Init(Transform target)
    {
        player = target;
        InitDirection();
    }

    private void InitDirection()
    {
        if (player == null)
        {
            moveDir = Vector3.left;
            RotateToDirection(moveDir);
            return;
        }

        if (!hasLockedTargetPos || !lockDirectionOnSpawn)
        {
            lockedTargetPos = player.position;
            hasLockedTargetPos = true;
        }

        Vector3 from = transform.position;
        Vector3 to = lockedTargetPos;
        if (keepBulletZPlane) to.z = from.z;

        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.left;

        moveDir = dir.normalized;
        RotateToDirection(moveDir);
    }

    private void Update()
    {
        if (isDead) return;

        if (!lockDirectionOnSpawn && player != null)
        {
            hasLockedTargetPos = false;
            InitDirection();
        }

        if (Time.time >= deathTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 pos = transform.position + moveDir * (speed * Time.deltaTime);
        if (keepBulletZPlane) pos.z = transform.position.z;
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead || other == null) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // Player
        if (other.CompareTag(playerTag))
        {
            isDead = true;
            EventBus.Raise(new PlayerHitEvent(damage, hitPoint));
            SpawnImpact(hitPoint);
            Destroy(gameObject);
            return;
        }

        // Env
        if (environmentLayer != -1 && other.gameObject.layer == environmentLayer)
        {
            isDead = true;

            // if this env is a road box collider, snap to its top (optional)
            Vector3 surfacePoint = hitPoint;
            surfacePoint.y = other.bounds.max.y;

            Vector3 surfaceNormal = Vector3.up;

            var receiver = other.GetComponentInParent<IEnvImpactReceiver>();
            if (receiver != null)
                receiver.OnEnvImpact(envEffectId, surfacePoint, surfaceNormal, other.gameObject);

            SpawnImpact(surfacePoint);
            Destroy(gameObject);
        }
    }

    private void SpawnImpact(Vector3 worldPos)
    {
        if (impactVfxPrefab == null) return;

        var fx = Instantiate(impactVfxPrefab, worldPos, Quaternion.identity);
        if (impactVfxLifetime > 0f) Destroy(fx, impactVfxLifetime);
    }

    private void RotateToDirection(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }
}

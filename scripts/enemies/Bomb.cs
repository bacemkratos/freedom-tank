using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Movement")]
    public float speed = 12f;
    public bool lockDirectionOnSpawn = true;

    [Header("Rotation")]
    public float rotationOffset = -90f;

    [Header("Gameplay")]
    public int damage = 5;

    [Header("Explosion (Visual Only)")]
    public GameObject explosionPrefab;
    public float destroyAfter = 6f;

    [Header("Layers/Tags")]
    public string playerTag = "Player";            // <-- your tag
    public string environmentLayerName = "Environment";

    private Vector3 moveDir = Vector3.down;
    private int environmentLayer;

    private void Awake()
    {
        environmentLayer = LayerMask.NameToLayer(environmentLayerName);
        Destroy(gameObject, destroyAfter);
    }

    private void Start()
    {
        // Fallback only (if Jet didn't assign player)
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) player = p.transform;
        }

        InitDirection();
    }

    // Call this from Jet right after Instantiate for perfect reliability
    public void Init(Transform target)
    {
        player = target;
        InitDirection();
    }

    private void InitDirection()
    {
        if (player == null)
        {
            moveDir = Vector3.down;
            return;
        }

        // 2.5D: aim in XY only
        Vector3 from = transform.position;
        Vector3 to = player.position;

        Vector2 dir2 = new Vector2(to.x - from.x, to.y - from.y);
        if (dir2.sqrMagnitude < 0.0001f) dir2 = Vector2.down;
        dir2.Normalize();

        moveDir = new Vector3(dir2.x, dir2.y, 0f);
        RotateToDirection(moveDir);
    }

    private void Update()
    {
        if (!lockDirectionOnSpawn && player != null)
        {
            Vector3 from = transform.position;
            Vector3 to = player.position;

            Vector2 dir2 = new Vector2(to.x - from.x, to.y - from.y);
            if (dir2.sqrMagnitude > 0.0001f)
            {
                dir2.Normalize();
                moveDir = new Vector3(dir2.x, dir2.y, 0f);
                RotateToDirection(moveDir);
            }
        }

        transform.position += moveDir * speed * Time.deltaTime;
    }

    private void RotateToDirection(Vector3 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    // ---------- Collision (3D) ----------
    private void OnTriggerEnter(Collider other)
    {
        HandleHit3D(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit3D(collision.collider);
    }

    private void HandleHit3D(Collider other)
    {
        if (other == null) return;

        // Player hit
        if (other.CompareTag(playerTag))
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            EventBus.Raise(new PlayerHitEvent(damage, hitPoint)); // ✅ damage via event
            SpawnExplosion(hitPoint);
            Destroy(gameObject);
            return;
        }

        // Environment hit (visual only)
        if (other.gameObject.layer == environmentLayer)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            SpawnExplosion(hitPoint);
            Destroy(gameObject);
        }
    }

    // ---------- Collision (2D) ----------
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit2D(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit2D(collision.collider);
    }

    private void HandleHit2D(Collider2D other)
    {
        if (other == null) return;

        if (other.CompareTag(playerTag))
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);

            EventBus.Raise(new PlayerHitEvent(damage, hitPoint)); // ✅ damage via event
            SpawnExplosion(hitPoint);
            Destroy(gameObject);
            return;
        }

        if (other.gameObject.layer == environmentLayer)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            SpawnExplosion(hitPoint);
            Destroy(gameObject);
        }
    }

    private void SpawnExplosion(Vector3 worldPos)
    {
        if (explosionPrefab == null) return;
        Instantiate(explosionPrefab, worldPos, Quaternion.identity);
    }
}

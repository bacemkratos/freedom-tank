using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 80f;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private int damage = 1;

    [Header("Behavior")]
    public bool destroyOnHit = true;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Screen Bounds")]
    [Tooltip("Extra margin before destroying bullet outside screen")]
    [SerializeField] private float screenMargin = 0.1f;

    private float _deathTime;
    private Camera _cam;

    private void OnEnable()
    {
        _deathTime = Time.time + lifeTime;
        _cam = Camera.main;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * (speed * dt);

        // --- Anti-tunneling raycast ---
        if (Physics.Raycast(start, transform.forward, out RaycastHit hit, speed * dt, hitMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;

            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                Vector3 normal = -transform.forward;
                damageable.TakeDamage(new DamageInfo(damage, hit.point, normal, gameObject));
            }

            if (destroyOnHit)
                Destroy(gameObject);

            return;
        }

        transform.position = end;

        // --- ✅ Destroy if outside screen ---
        if (IsOutsideScreen())
        {
            Destroy(gameObject);
            return;
        }

        // --- Lifetime fallback ---
        if (Time.time >= _deathTime)
            Destroy(gameObject);
    }

    private bool IsOutsideScreen()
    {
        if (_cam == null) return false;

        Vector3 vp = _cam.WorldToViewportPoint(transform.position);

        // Behind camera
        if (vp.z < 0f)
            return true;

        // Outside screen with margin
        if (vp.x < -screenMargin || vp.x > 1f + screenMargin ||
            vp.y < -screenMargin || vp.y > 1f + screenMargin)
            return true;

        return false;
    }

    public void SetDamage(int value) => damage = value;
    public void SetSpeed(float value) => speed = value;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 normal = (transform.position - hitPoint).normalized;

            damageable.TakeDamage(new DamageInfo(damage, hitPoint, normal, gameObject));

            if (destroyOnHit)
                Destroy(gameObject);
        }
    }
}

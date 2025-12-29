using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable, IEnemySpawnable
{
    [Header("Base Health")]
    [SerializeField] protected int maxHP = 3;

    protected int hp;

    protected Transform player;
    protected Camera cam;
    protected SpawnSide spawnSide;

    protected EnemyScreenLifecycle lifecycle;

    protected bool isDying;

    protected virtual void Awake()
    {
        hp = maxHP;

        lifecycle = GetComponent<EnemyScreenLifecycle>();
        if (lifecycle == null)
            lifecycle = gameObject.AddComponent<EnemyScreenLifecycle>();
    }

    // Generic spawn init for ALL enemies
    public virtual void OnSpawned(EnemySpawnContext ctx)
    {
        player = ctx.player;
        cam = ctx.cam != null ? ctx.cam : Camera.main;
        spawnSide = ctx.side;

        transform.position = ctx.spawnPos;

        // Face inward (common for left/right spawns)
        float yRot = (spawnSide == SpawnSide.Right) ? 180f : 0f;
        transform.rotation = Quaternion.Euler(0f, yRot, 0f);

        // init lifecycle
        if (lifecycle != null) lifecycle.Init(cam);

        // allow child to reset its state machine
        OnSpawnedInternal(ctx);
    }

    protected virtual void OnSpawnedInternal(EnemySpawnContext ctx) { }

    // old int API still supported
    public void TakeDamage(int damage)
    {
        TakeDamage(new DamageInfo(
            damage,
            Vector3.zero,
            Vector3.zero,
            null
        ));
    }


    // unified DamageInfo API
    public virtual void TakeDamage(DamageInfo info)
    {
        if (isDying) return;

        hp -= info.damage;
        if (hp <= 0)
            Die();
        else
            OnDamaged(info);
    }

    protected virtual void OnDamaged(DamageInfo info) { }

    protected virtual void Die()
    {
        if (isDying) return;
        isDying = true;

        EventBus.Raise(new EnemyDestroyedEvent(gameObject));

        Destroy(gameObject);
    }

    // use when you want VFX then destroy
    protected void DieAfterExplosionAt(Vector3 pos, GameObject explosionPrefab)
    {
        if (isDying) return;
        isDying = true;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, pos, Quaternion.identity);

        EventBus.Raise(new EnemyDestroyedEvent(gameObject));

        Destroy(gameObject);
    }
}

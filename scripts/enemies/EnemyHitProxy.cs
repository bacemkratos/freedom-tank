using UnityEngine;

/// <summary>
/// Put this on any CHILD collider (turret, barrel, weakpoint).
/// When the bullet hits the child collider, this forwards damage to the parent EnemyBase.
/// </summary>
public class EnemyHitProxy : MonoBehaviour, IDamageable
{
    private EnemyBase owner;

    private void Awake()
    {
        owner = GetComponentInParent<EnemyBase>();
        if (owner == null)
            Debug.LogError($"EnemyHitProxy: No EnemyBase found in parents of {name}");
    }

    public void TakeDamage(int damage)
    {
        if (owner != null)
            owner.TakeDamage(damage);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (owner != null)
            owner.TakeDamage(info);
    }
}

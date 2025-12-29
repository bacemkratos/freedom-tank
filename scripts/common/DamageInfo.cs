using UnityEngine;

public readonly struct DamageInfo
{
    public readonly int damage;
    public readonly Vector3 hitPoint;
    public readonly Vector3 hitNormal;
    public readonly GameObject source; // bullet, rocket, etc.

    public DamageInfo(int damage, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
    {
        this.damage = damage;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.source = source;
    }
}

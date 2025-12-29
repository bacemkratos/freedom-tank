using UnityEngine;

public readonly struct LevelStartEvent : IGameEvent { }
public readonly struct LevelEndEvent : IGameEvent { }

// ✅ Sticky: new subscribers immediately receive current env speed
public struct EnvironmentSpeedChangedEvent : IStickyEvent
{
    public float speed;
    public EnvironmentSpeedChangedEvent(float speed) => this.speed = speed;
}

// ✅ Sticky: new subscribers immediately receive current tank speed
public struct TankSpeedChangedEvent : IStickyEvent
{
    public float speed;
    public TankSpeedChangedEvent(float speed) => this.speed = speed;
}

public struct WeaponChangedEvent : IGameEvent
{
    public string weaponId;
    public WeaponChangedEvent(string weaponId) => this.weaponId = weaponId;
}

public struct WeaponAmmoChangedEvent : IGameEvent
{
    public string weaponId;
    public int ammo;
    public bool infinite;
    public WeaponAmmoChangedEvent(string weaponId, int ammo, bool infinite)
    {
        this.weaponId = weaponId;
        this.ammo = ammo;
        this.infinite = infinite;
    }
}

public struct PlayerHitEvent : IGameEvent
{
    public int damage;
    public Vector3 hitPoint;
    public PlayerHitEvent(int damage, Vector3 hitPoint)
    {
        this.damage = damage;
        this.hitPoint = hitPoint;
    }
}

public readonly struct EnemyDestroyedEvent : IGameEvent
{
    public readonly GameObject enemy;
    public EnemyDestroyedEvent(GameObject enemy) => this.enemy = enemy;
}

public readonly struct AllWavesCompletedEvent : IGameEvent { }

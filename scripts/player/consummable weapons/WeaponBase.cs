using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string weaponId = "weapon";
    public string WeaponId => weaponId;

    [Header("Ammo")]
    [SerializeField] private bool infiniteAmmo = false;
    [SerializeField] private int ammo = 0;
    public bool InfiniteAmmo => infiniteAmmo;
    public int Ammo => ammo;

    [Header("Firing")]
    [SerializeField] protected float fireRate = 8f; // shots per second
    private float _nextFireTime;

    public bool IsUsable => InfiniteAmmo || ammo > 0;

    public virtual void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public bool TryFire()
    {
        if (Time.time < _nextFireTime) return false;
        if (!IsUsable) return false;

        _nextFireTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));

        Fire();

        if (!InfiniteAmmo)
        {
            ammo = Mathf.Max(0, ammo - 1);
            EventBus.Raise(new WeaponAmmoChangedEvent(weaponId, ammo, infiniteAmmo));
        }

        return true;
    }

    // If you pick ammo from pickups, call this
    public void AddAmmo(int amount)
    {
        if (InfiniteAmmo) return;
        ammo = Mathf.Max(0, ammo + amount);
        EventBus.Raise(new WeaponAmmoChangedEvent(weaponId, ammo, infiniteAmmo));
    }

    public void SetAmmo(int value)
    {
        if (InfiniteAmmo) return;
        ammo = Mathf.Max(0, value);
        EventBus.Raise(new WeaponAmmoChangedEvent(weaponId, ammo, infiniteAmmo));
    }

    protected abstract void Fire();
}

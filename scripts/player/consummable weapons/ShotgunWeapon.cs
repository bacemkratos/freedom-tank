using UnityEngine;

public class ShotgunWeapon : WeaponBase
{
    // In Inspector: infiniteAmmo = false, ammo = e.g. 20, weaponId = "shotgun"

    [SerializeField] private int pellets = 6;
    [SerializeField] private float spread = 8f;

    protected override void Fire()
    {
        Debug.Log($"Shotgun Fire: pellets={pellets}, spread={spread}");
        // TODO: spawn multiple pellets with spread
    }
}

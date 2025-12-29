using UnityEngine;
public enum SpinAxis { X, Y, Z }
public class MinigunWeapon : WeaponBase
{

 


    [Header("Spawn")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private BulletProjectile bulletPrefab;

    [Header("Bullet Tuning")]
    [SerializeField] private float bulletSpeed = 90f;
    [SerializeField] private int bulletDamage = 1;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem smoke;

    [Header("SFX")]
    [SerializeField] private AudioSource loopAudio; // set loop=true in inspector
    [SerializeField] private AudioClip shotOneShot; // optional: if you prefer per-shot sound
    [SerializeField] private bool useLoopAudio = true;

    [Header("Barrel Spin")]
    [SerializeField] private Transform barrelToSpin;
    [SerializeField] private float spinSpeed = 1800f; // degrees/sec while firing
    [SerializeField] private float spinDownSpeed = 900f; // degrees/sec when releasing
    [SerializeField] private SpinAxis spinAxis = SpinAxis.X;

    [Header("Aim Fix")]
    [Tooltip("If muzzlePoint.forward isn't the true shooting direction, rotate it here.")]
    [SerializeField] private Vector3 muzzleEulerOffset = Vector3.zero;

    private bool _isFiringHeld;
    private float _currentSpin;

    private void Update()
    {
        // Hold-to-fire behavior (you can move this input out later)
        _isFiringHeld = Input.GetMouseButton(0);

        if (_isFiringHeld)
        {
            // Fire as fast as WeaponBase allows (fireRate)
            TryFire();

            StartFiringFX();
            SpinUp();
        }
        else
        {
            StopFiringFX();
            SpinDown();
        }

        SpinBarrelVisual();
    }

    protected override void Fire()
    {
        if (!muzzlePoint || !bulletPrefab) return;

        Quaternion rot = muzzlePoint.rotation * Quaternion.Euler(muzzleEulerOffset);

        var bullet = Instantiate(bulletPrefab, muzzlePoint.position, rot);
        bullet.SetSpeed(bulletSpeed);
        bullet.SetDamage(bulletDamage);

        // If you want per-shot audio instead of loop:
        if (!useLoopAudio && shotOneShot && loopAudio)
            loopAudio.PlayOneShot(shotOneShot);
    }

    private void StartFiringFX()
    {
        if (muzzleFlash && !muzzleFlash.isPlaying) muzzleFlash.Play();
        if (smoke && !smoke.isPlaying) smoke.Play();

        if (useLoopAudio && loopAudio && !loopAudio.isPlaying)
            loopAudio.Play();
    }

    private void StopFiringFX()
    {
        // For particles, better to stop emitting but let particles finish
        if (muzzleFlash && muzzleFlash.isPlaying) muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (smoke && smoke.isPlaying) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (useLoopAudio && loopAudio && loopAudio.isPlaying)
            loopAudio.Stop();
    }

    private void SpinUp()
    {
        _currentSpin = Mathf.MoveTowards(_currentSpin, spinSpeed, spinDownSpeed * Time.deltaTime * 2f);
    }

    private void SpinDown()
    {
        _currentSpin = Mathf.MoveTowards(_currentSpin, 0f, spinDownSpeed * Time.deltaTime);
    }

    private void SpinBarrelVisual()
    {
        if (!barrelToSpin) return;

        Vector3 axis = spinAxis switch
        {
            SpinAxis.X => Vector3.right,
            SpinAxis.Y => Vector3.up,
            _ => Vector3.forward
        };

        barrelToSpin.Rotate(axis, _currentSpin * Time.deltaTime, Space.Self);
    }
}

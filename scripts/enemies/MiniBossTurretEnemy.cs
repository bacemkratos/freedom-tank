using System.Collections;
using UnityEngine;

public class MiniBossTurretEnemy : EnemyBase
{
    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Env Pause/Resume")]
    public bool pauseEnvOnEnter = true;
    public bool resumeEnvOnDeath = true;

    [Tooltip("If true, miniboss won't auto-despawn once it entered screen.")]
    public bool disableDespawnOnceEntered = true;

    [Header("Entry Detection (USE COLLIDER)")]
    [Tooltip("Assign a Collider for view detection. If empty, auto-finds in root/children.")]
    public Collider entryCollider;

    [Header("Enter View (ANY visibility)")]
    [Range(0f, 0.2f)] public float viewportMargin = 0.02f;
    public float enterEventDelaySeconds = 0.25f;
    public bool disableDespawnOnVisible = true;

    [Header("Facing (Flip ROOT)")]
    public bool prefabFacesPositiveX = true;

    [Header("Turret (Rotates on Z)")]
    public Transform turret;
    public float turretTurnSpeed = 10f;

    [Header("Firing")]
    public bool canFire = true;
    public float prepareToFireSeconds = 0.6f;
    public float fireIntervalSeconds = 2.0f;
    public float fireIntervalJitter = 0.25f;
    public float secondShotDelay = 0.08f;

    public GameObject bulletPrefab;
    public Transform muzzle1;
    public Transform muzzle2;

    [Header("Muzzle VFX (Prefab)")]
    public GameObject muzzleVfxPrefab;

    [Header("Damage VFX (Already in Prefab, Inactive)")]
    public GameObject vfxDamaged50Obj;
    public GameObject vfxDamaged75Obj;

    [Header("Death VFX")]
    public GameObject explosionPrefab;

    [Header("Death Cleanup")]
    [Tooltip("Fallback destroy if pooling isn't working.")]
    public float destroyFallbackAfterSeconds = 0.1f;

    [Header("Turret Shake (Z)")]
    public float shakeDuration = 0.12f;
    public float shakeAngle = 2.5f;

    [Header("Landing (No Gravity)")]
    public bool snapDownToEnv = true;
    public float fallSpeed = 18f;
    public float groundRayDistance = 50f;
    public float groundSurfaceOffset = 0.02f;
    public string envLayerName = "env";

    // Env speed tracking
    private float currentEnvSpeed = 10f;
    private float cachedSpeedBeforePause = 10f;
    private bool envPausedByMe;

    // Enter detection
    private bool hasEnteredView;
    private bool enterEventSent;
    private float enteredViewTime;

    private bool fired50;
    private bool fired75;

    private float fireTimer;
    private float nextFireDelay;

    private Coroutine fireRoutine;
    private Coroutine shakeRoutine;
    private Coroutine landingRoutine;

    private int envLayerMask;

    protected override void Awake()
    {
        base.Awake();

        if (turret == null) turret = transform;
        if (muzzle1 == null) muzzle1 = turret;
        if (muzzle2 == null) muzzle2 = turret;

        if (entryCollider == null) entryCollider = GetComponentInChildren<Collider>(true);
        if (entryCollider == null) entryCollider = GetComponent<Collider>();

        envLayerMask = LayerMask.GetMask(envLayerName);

        ResetFireLoop();
        fireTimer = -prepareToFireSeconds;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EnvironmentSpeedChangedEvent>(OnEnvSpeedChanged);
    }

    private void OnEnvSpeedChanged(EnvironmentSpeedChangedEvent evt)
    {
        currentEnvSpeed = evt.speed;
        if (Mathf.Abs(evt.speed) > 0.0001f)
            cachedSpeedBeforePause = evt.speed;
    }

    protected override void OnSpawnedInternal(EnemySpawnContext ctx)
    {
        fired50 = false;
        fired75 = false;

        envPausedByMe = false;

        hasEnteredView = false;
        enterEventSent = false;
        enteredViewTime = 0f;

        if (cam == null) cam = Camera.main;

        FlipWholeRootBySpawnSide();

        if (vfxDamaged50Obj != null) vfxDamaged50Obj.SetActive(false);
        if (vfxDamaged75Obj != null) vfxDamaged75Obj.SetActive(false);

        StopAndNull(ref fireRoutine);
        StopAndNull(ref shakeRoutine);
        StopAndNull(ref landingRoutine);

        if (snapDownToEnv)
            landingRoutine = StartCoroutine(FallDownUntilEnv());

        fireTimer = -prepareToFireSeconds;
        ResetFireLoop();
        canFire = true;

        if (debugLogs)
            Debug.Log($"[MiniBoss] Spawned side={spawnSide} entryCollider={(entryCollider ? entryCollider.name : "NULL")}");
    }

    private void StopAndNull(ref Coroutine c)
    {
        if (c != null) StopCoroutine(c);
        c = null;
    }

    private void FlipWholeRootBySpawnSide()
    {
        bool wantsPositiveX = (spawnSide == SpawnSide.Left);
        if (!prefabFacesPositiveX) wantsPositiveX = !wantsPositiveX;

        Vector3 s = transform.localScale;
        s.x = wantsPositiveX ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        transform.localScale = s;
    }

    private IEnumerator FallDownUntilEnv()
    {
        while (!isDying)
        {
            Vector3 origin = entryCollider != null ? entryCollider.bounds.center : transform.position;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRayDistance, envLayerMask, QueryTriggerInteraction.Ignore))
            {
                float desiredY = hit.point.y + groundSurfaceOffset;
                Vector3 p = transform.position;

                if (p.y <= desiredY + 0.01f)
                {
                    p.y = desiredY;
                    transform.position = p;
                    yield break;
                }

                p.y -= fallSpeed * Time.deltaTime;
                if (p.y < desiredY) p.y = desiredY;
                transform.position = p;
            }
            else
            {
                transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
            }

            yield return null;
        }
    }

    private void Update()
    {
        if (isDying) return;
        if (player == null) return;
        if (cam == null) cam = Camera.main;

        // 1) Detect first visible
        if (!hasEnteredView && entryCollider != null && IsColliderAnyInsideView(entryCollider))
        {
            hasEnteredView = true;
            enteredViewTime = Time.time;

            if (disableDespawnOnceEntered && disableDespawnOnVisible && lifecycle != null)
                lifecycle.DisableDespawn = true;

            if (debugLogs)
                Debug.Log("[MiniBoss] Entered camera view (partial). Timer started.");
        }

        // 2) After delay, pause env ONCE
        if (hasEnteredView && !enterEventSent && (Time.time - enteredViewTime) >= enterEventDelaySeconds)
        {
            enterEventSent = true;

            if (disableDespawnOnceEntered && lifecycle != null)
                lifecycle.DisableDespawn = true;

            if (pauseEnvOnEnter)
                PauseEnvironment();

            if (debugLogs)
                Debug.Log($"[MiniBoss] Enter delay reached ({enterEventDelaySeconds}s) -> pause applied.");
        }

        RotateTurretToPlayerZ();

        if (!canFire) return;
        if (!enterEventSent) return;

        fireTimer += Time.deltaTime;
        if (fireTimer >= nextFireDelay)
        {
            fireTimer = 0f;
            ResetFireLoop();

            if (fireRoutine == null)
                fireRoutine = StartCoroutine(FireBurst2());
        }
    }

    private bool IsColliderAnyInsideView(Collider c)
    {
        if (c == null || cam == null) return false;

        Bounds b = c.bounds;
        Vector3 cen = b.center;
        Vector3 ext = b.extents;
        float z = cen.z;

        Vector3[] corners = new Vector3[4]
        {
            new Vector3(cen.x - ext.x, cen.y - ext.y, z),
            new Vector3(cen.x - ext.x, cen.y + ext.y, z),
            new Vector3(cen.x + ext.x, cen.y - ext.y, z),
            new Vector3(cen.x + ext.x, cen.y + ext.y, z),
        };

        float m = viewportMargin;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 v = cam.WorldToViewportPoint(corners[i]);
            if (v.z <= 0f) continue;

            if (v.x >= m && v.x <= 1f - m && v.y >= m && v.y <= 1f - m)
                return true;
        }

        return false;
    }

    private void PauseEnvironment()
    {
        if (envPausedByMe) return;

        if (Mathf.Abs(currentEnvSpeed) > 0.0001f)
            cachedSpeedBeforePause = currentEnvSpeed;

        envPausedByMe = true;

        if (debugLogs)
            Debug.Log($"[MiniBoss] Pause env -> speed 0 (cached={cachedSpeedBeforePause})");

        EventBus.Raise(new EnvironmentSpeedChangedEvent(0f));
    }

    private void ResumeEnvironment()
    {
        if (!envPausedByMe) return; // ✅ only resume if WE paused it
        envPausedByMe = false;

        if (debugLogs)
            Debug.Log($"[MiniBoss] Resume env -> speed {cachedSpeedBeforePause}");

        EventBus.Raise(new EnvironmentSpeedChangedEvent(cachedSpeedBeforePause));
    }

    private void ResetFireLoop()
    {
        nextFireDelay = fireIntervalSeconds + Random.Range(-fireIntervalJitter, fireIntervalJitter);
        if (nextFireDelay < 0.15f) nextFireDelay = 0.15f;
    }

    private void RotateTurretToPlayerZ()
    {
        if (turret == null) return;

        Vector3 dir = player.position - turret.position;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (transform.lossyScale.x < 0f)
            ang += 180f;

        Quaternion target = Quaternion.Euler(0f, 0f, ang);
        turret.rotation = Quaternion.Slerp(turret.rotation, target, turretTurnSpeed * Time.deltaTime);
    }

    private IEnumerator FireBurst2()
    {
        FireOne(muzzle1);
        yield return new WaitForSeconds(secondShotDelay);
        FireOne(muzzle2);
        fireRoutine = null;
    }

    private void FireOne(Transform muzzle)
    {
        if (muzzle == null || bulletPrefab == null) return;

        Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);

        if (muzzleVfxPrefab != null)
            Instantiate(muzzleVfxPrefab, muzzle.position, muzzle.rotation);

        if (shakeRoutine == null)
            shakeRoutine = StartCoroutine(ShakeTurretZ());
    }

    private IEnumerator ShakeTurretZ()
    {
        if (turret == null) yield break;

        float t = 0f;
        Quaternion start = turret.localRotation;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float a = Random.Range(-shakeAngle, shakeAngle);
            turret.localRotation = start * Quaternion.Euler(0f, 0f, a);
            yield return null;
        }

        turret.localRotation = start;
        shakeRoutine = null;
    }

    protected override void OnDamaged(DamageInfo info)
    {
        base.OnDamaged(info);

        float hp01 = (maxHP <= 0) ? 0f : (float)hp / maxHP;

        if (!fired50 && hp01 <= 0.50f)
        {
            fired50 = true;
            if (vfxDamaged50Obj != null) vfxDamaged50Obj.SetActive(true);
        }

        if (!fired75 && hp01 <= 0.25f)
        {
            fired75 = true;
            if (vfxDamaged75Obj != null) vfxDamaged75Obj.SetActive(true);
        }
    }

    protected override void Die()
    {
        if (isDying) return;
        isDying = true;

        // stop any delayed enter logic
        hasEnteredView = true;
        enterEventSent = true;

        canFire = false;

        StopAndNull(ref landingRoutine);
        StopAndNull(ref fireRoutine);
        StopAndNull(ref shakeRoutine);

        // Resume only if we paused
        if (resumeEnvOnDeath)
            ResumeEnvironment();

        // Spawn explosion
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // ✅ ALWAYS remove miniboss
        // If you have pooling, prefer lifecycle despawn. Otherwise destroy.
        if (lifecycle != null)
        {
            // Option A: if your lifecycle has a despawn method, call it:
            // lifecycle.DespawnNow();
            // Since we don't know the method name, safest fallback:
            Destroy(gameObject, destroyFallbackAfterSeconds);
        }
        else
        {
            Destroy(gameObject, destroyFallbackAfterSeconds);
        }
    }

}

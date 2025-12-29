using UnityEngine;

public class DroneEnemy : EnemyBase
{
    [Header("Environment Layer")]
    [SerializeField] private string envLayerName = "env";
    private int envLayer;

    [Header("Lane / Depth Lock")]
    public bool lockZToPlayer = true;

    [Header("Movement")]
    public float cruiseSpeed = 6f;
    public float engageDistance = 30f;

    [Tooltip("Base hover offset. X used as side distance. Y is handled separately.")]
    public Vector3 hoverOffset = new Vector3(12f, 0f, 0f);
    public float hoverSnapSpeed = 6f;

    [Header("Keep Distance From Player")]
    public float minDistanceToPlayerX = 20f;
    public float minAbovePlayerY = 10f;
    public float laneYOffset = 2f;

    [Header("Clamp Inside Screen (X Only)")]
    public bool clampXToScreen = true;
    public float screenXMargin = 0.8f;

    [Header("Enemy Avoidance (Layer = enemy)")]
    public float avoidEnemyRadius = 4.5f;
    public float avoidStrength = 5.0f;

    [Tooltip("Hard minimum separation between drones (prevents overlap).")]
    public float minEnemySeparation = 3.0f;

    [Tooltip("Max avoidance offset applied to hover target (prevents big jumps).")]
    public float maxAvoidOffset = 2.2f;

    [Tooltip("Smooth time for avoidance offset (bigger = smoother, less jitter).")]
    public float avoidSmoothTime = 0.25f;

    [Tooltip("If your enemy colliders are triggers, this MUST be true.")]
    public bool includeTriggerEnemies = true;

    [Header("Aim / Fire")]
    public float aimTurnSpeed = 10f;
    public float prepareToFireSeconds = 0.35f;
    public float fireIntervalSeconds = 3.5f;
    public float fireIntervalJitter = 0.5f;

    [Header("Projectile / Muzzle")]
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public GameObject muzzleVfxPrefab;

    [Header("Death / VFX")]
    public GameObject smokeFirePrefab;
    public GameObject explosionPrefab;
    public float explodeAfterSeconds = 5f;
    private GameObject smokeInstance;

    [Header("Crash Motion")]
    public Vector2 crashXSpeedRange = new Vector2(1.5f, 4.0f);
    public Vector2 crashDownSpeedRange = new Vector2(2.5f, 6.5f);
    public Vector2 crashZSpeedRange = new Vector2(3.0f, 10.0f);
    public Vector3 crashAcceleration = new Vector3(0f, 6f, 12f);

    [Header("Crash Impact (Env Hit)")]
    public float crashHitRadius = 0.55f;
    public float crashSurfaceOffset = 0.03f;
    public bool explodeOnEnvHit = true;

    [Header("MoveToHover Entry (Fix slow firing)")]
    public float hoverArriveDistance = 1.25f;
    public float maxMoveToHoverSeconds = 1.2f;

    private enum State { EnterCruise, MoveToHover, FiringLoop, Destroying }
    private State state;

    private float fireTimer;
    private float nextFireDelay;
    private bool hasExploded;

    private Vector3 crashVel;
    private float crashTime;

    private int enemyLayer;
    private float moveToHoverTimer;

    // smoothed avoidance
    private Vector3 _avoidOffsetSmoothed;
    private Vector3 _avoidOffsetVel;

    protected override void Awake()
    {
        base.Awake();

        envLayer = LayerMask.NameToLayer(envLayerName);
        if (envLayer == -1)
            Debug.LogError($"DroneEnemy: Layer '{envLayerName}' not found.");

        enemyLayer = LayerMask.NameToLayer("enemy");
        if (enemyLayer == -1)
            Debug.LogError("DroneEnemy: Layer 'enemy' not found.");

        if (bulletSpawnPoint == null) bulletSpawnPoint = transform;
        ResetFireLoop();
    }

    protected override void OnSpawnedInternal(EnemySpawnContext ctx)
    {
        state = State.EnterCruise;
        hasExploded = false;
        moveToHoverTimer = 0f;

        _avoidOffsetSmoothed = Vector3.zero;
        _avoidOffsetVel = Vector3.zero;

        CancelInvoke(nameof(ExplodeSelf));

        if (lifecycle != null) lifecycle.DisableDespawn = false;

        ResetFireLoop();
        ApplyZLock();
    }

    private void Update()
    {
        if (state == State.Destroying)
        {
            DoDestroyingFall();
            return;
        }

        if (player == null)
        {
            MoveForwardX(cruiseSpeed);
            if (lifecycle != null) lifecycle.Tick();
            return;
        }

        ApplyZLock();

        switch (state)
        {
            case State.EnterCruise:
                MoveForwardX(cruiseSpeed);

                if (lifecycle != null && !lifecycle.HasEnteredScreen)
                    break;

                if (ShouldEngagePlayer())
                {
                    state = State.MoveToHover;
                    fireTimer = 0f;
                    moveToHoverTimer = 0f;
                }
                break;

            case State.MoveToHover:
                {
                    moveToHoverTimer += Time.deltaTime;

                    Vector3 hoverPos = GetHoverWorldPositionSmoothed();
                    transform.position = Vector3.MoveTowards(transform.position, hoverPos, hoverSnapSpeed * Time.deltaTime);

                    RotateToFacePlayer();

                    float dist = Vector3.Distance(transform.position, hoverPos);
                    if (dist <= hoverArriveDistance || moveToHoverTimer >= maxMoveToHoverSeconds)
                    {
                        state = State.FiringLoop;
                        fireTimer = -prepareToFireSeconds;
                        ResetFireLoop();
                    }
                    break;
                }

            case State.FiringLoop:
                {
                    Vector3 hoverPos = GetHoverWorldPositionSmoothed();
                    transform.position = Vector3.Lerp(transform.position, hoverPos, Time.deltaTime * hoverSnapSpeed);

                    RotateToFacePlayer();

                    fireTimer += Time.deltaTime;
                    if (fireTimer >= nextFireDelay)
                    {
                        FireOneShot();
                        ResetFireLoop();
                        fireTimer = 0f;
                    }
                    break;
                }
        }

        if (lifecycle != null) lifecycle.Tick();
    }

    private void ApplyZLock()
    {
        if (!lockZToPlayer || player == null) return;
        Vector3 p = transform.position;
        p.z = player.position.z;
        transform.position = p;
    }

    private Vector3 GetHoverWorldPositionSmoothed()
    {
        Vector3 baseTarget = GetHoverWorldPositionBase();

        Vector3 rawAvoid = ComputeEnemyAvoidanceOffsetXY(baseTarget);
        rawAvoid = Vector3.ClampMagnitude(rawAvoid, maxAvoidOffset);

        _avoidOffsetSmoothed = Vector3.SmoothDamp(
            _avoidOffsetSmoothed,
            rawAvoid,
            ref _avoidOffsetVel,
            Mathf.Max(0.01f, avoidSmoothTime)
        );

        Vector3 target = baseTarget + _avoidOffsetSmoothed;

        // Keep above player
        float minY = player.position.y + minAbovePlayerY;
        if (target.y < minY) target.y = minY;

        if (lockZToPlayer) target.z = player.position.z;
        return target;
    }

    private Vector3 GetHoverWorldPositionBase()
    {
        float xSide = Mathf.Abs(hoverOffset.x);
        float signedX = (spawnSide == SpawnSide.Right) ? -xSide : xSide;

        float desiredX = player.position.x + signedX;

        float dx = desiredX - player.position.x;
        if (Mathf.Abs(dx) < minDistanceToPlayerX)
            desiredX = player.position.x + Mathf.Sign(dx == 0 ? signedX : dx) * minDistanceToPlayerX;

        float desiredY = player.position.y + Mathf.Max(minAbovePlayerY, laneYOffset);

        float desiredZ = lockZToPlayer ? player.position.z : transform.position.z;
        Vector3 target = new Vector3(desiredX, desiredY, desiredZ);

        if (clampXToScreen)
            target.x = ClampXInsideScreen(target.x, target.z);

        if (lockZToPlayer) target.z = player.position.z;
        return target;
    }

    private Vector3 ComputeEnemyAvoidanceOffsetXY(Vector3 targetPos)
    {
        if (enemyLayer == -1) return Vector3.zero;

        int mask = 1 << enemyLayer;

        // ✅ IMPORTANT: if enemies are triggers, Ignore breaks avoidance.
        QueryTriggerInteraction q = includeTriggerEnemies
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        Collider[] hits = Physics.OverlapSphere(targetPos, avoidEnemyRadius, mask, q);

        Vector3 push = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (!c || c.transform == transform) continue;

            Vector3 other = c.ClosestPoint(targetPos);

            Vector3 away = (targetPos - other);
            away.z = 0f;

            float d = away.magnitude;
            if (d < 0.0001f) continue;

            // ✅ hard separation
            if (d < minEnemySeparation)
            {
                float t = 1f - (d / minEnemySeparation);
                push += away.normalized * (t * avoidStrength * 2.5f);
            }
            else
            {
                float t = 1f - (d / avoidEnemyRadius);
                push += away.normalized * (t * avoidStrength);
            }
        }

        push.z = 0f;
        return push;
    }

    private float ClampXInsideScreen(float x, float zPlane)
    {
        Camera cam = Camera.main;
        if (cam == null) return x;

        Ray rL = cam.ViewportPointToRay(new Vector3(0f, 0.5f, 0f));
        Ray rR = cam.ViewportPointToRay(new Vector3(1f, 0.5f, 0f));
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, zPlane));

        Vector3 left = Vector3.zero, right = Vector3.zero;
        float enterL, enterR;
        if (plane.Raycast(rL, out enterL)) left = rL.GetPoint(enterL);
        if (plane.Raycast(rR, out enterR)) right = rR.GetPoint(enterR);

        float minX = left.x + screenXMargin;
        float maxX = right.x - screenXMargin;
        return Mathf.Clamp(x, minX, maxX);
    }

    protected override void Die() => EnterDestroying();

    private void EnterDestroying()
    {
        if (state == State.Destroying) return;

        state = State.Destroying;
        if (lifecycle != null) lifecycle.DisableDespawn = true;

        crashTime = 0f;

        float dirX = (Random.value < 0.5f) ? -1f : 1f;
        float x = Random.Range(crashXSpeedRange.x, crashXSpeedRange.y) * dirX;
        float y = -Random.Range(crashDownSpeedRange.x, crashDownSpeedRange.y);
        float z = Random.Range(crashZSpeedRange.x, crashZSpeedRange.y);
        crashVel = new Vector3(x, y, z);

        if (smokeFirePrefab != null && smokeInstance == null)
            smokeInstance = Instantiate(smokeFirePrefab, transform.position, Quaternion.identity, transform);

        CancelInvoke(nameof(ExplodeSelf));
        Invoke(nameof(ExplodeSelf), Mathf.Max(0.05f, explodeAfterSeconds));
    }

    private void DoDestroyingFall()
    {
        crashTime += Time.deltaTime;

        crashVel.x += crashAcceleration.x * Time.deltaTime * Mathf.Sign(crashVel.x == 0f ? 1f : crashVel.x);
        crashVel.y -= Mathf.Abs(crashAcceleration.y) * Time.deltaTime;
        crashVel.z += Mathf.Abs(crashAcceleration.z) * Time.deltaTime;

        Vector3 startPos = transform.position;
        Vector3 delta = crashVel * Time.deltaTime;

        if (!hasExploded && envLayer != -1 && delta.sqrMagnitude > 0.0000001f)
        {
            int envMask = 1 << envLayer;

            if (Physics.SphereCast(startPos, crashHitRadius, delta.normalized, out RaycastHit hit, delta.magnitude, envMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * crashSurfaceOffset;

                if (explodeOnEnvHit)
                {
                    ExplodeSelf();
                    return;
                }

                crashVel = Vector3.zero;
                return;
            }
        }

        transform.position = startPos + delta;

        transform.rotation *= Quaternion.Euler(
            120f * Time.deltaTime,
            90f * Time.deltaTime,
            200f * Time.deltaTime
        );
    }

    private void ExplodeSelf()
    {
        if (hasExploded) return;
        hasExploded = true;

        CancelInvoke(nameof(ExplodeSelf));
        DieAfterExplosionAt(transform.position, explosionPrefab);
    }

    private void FireOneShot()
    {
        if (bulletPrefab == null) return;
        if (player == null) return;
        if (bulletSpawnPoint == null) return;

        if (muzzleVfxPrefab != null)
            Instantiate(muzzleVfxPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);

        GameObject go = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.identity);

        var proj = go.GetComponent<EnemyBulletProjectile>();
        if (proj != null)
        {
            proj.lockDirectionOnSpawn = true;
            proj.Init(player); // keeps prefab speed/damage
        }
        else
        {
            go.transform.rotation = bulletSpawnPoint.rotation;
        }
    }

    private void ResetFireLoop()
    {
        nextFireDelay = fireIntervalSeconds + Random.Range(-fireIntervalJitter, fireIntervalJitter);
        if (nextFireDelay < 0.05f) nextFireDelay = 0.05f;
    }

    private void MoveForwardX(float speed)
    {
        float dir = (spawnSide == SpawnSide.Left) ? 1f : -1f;
        transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
    }

    private bool ShouldEngagePlayer()
    {
        float dist = Mathf.Abs(transform.position.x - player.position.x);
        return dist <= engageDistance;
    }

    private void RotateToFacePlayer()
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        Quaternion lookZForward = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        Quaternion lookXForward = lookZForward * Quaternion.Euler(0f, -90f, 0f);

        Vector3 e = lookXForward.eulerAngles;
        e.x = NormalizeAngle(e.x);
        e.y = NormalizeAngle(e.y);
        e.z = NormalizeAngle(e.z);

        e.x = Mathf.Clamp(e.x, -90f, 90f);
        e.z = Mathf.Clamp(e.z, -90f, 90f);

        Quaternion targetRot = Quaternion.Euler(e.x, e.y, e.z);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * aimTurnSpeed);
    }

    private float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        return a;
    }
}

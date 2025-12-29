using UnityEngine;

public class JetEnemy : EnemyBase
{
    [Header("Environment Layer")]
    [SerializeField] private string envLayerName = "env";
    private int envLayer;

    [Header("Death / Destroying VFX")]
    public GameObject smokeFirePrefab;
    public GameObject explosionPrefab;

    [Tooltip("If the jet never hits the env, explode anyway after this many seconds once crashing starts.")]
    public float explodeFailSafeSeconds = 6f;

    private GameObject smokeInstance;

    [Header("Crash Motion (Destroying)")]
    public float crashInitialZSpeed = 15f;
    public float crashZAcceleration = 60f;
    public float crashInitialDownSpeed = 10f;
    public float crashDownAcceleration = 40f;

    [Header("Crash Motion (X Drift)")]
    public float crashInitialXSpeed = 0f;
    public float crashXAcceleration = 0f;
    public float crashXRandomDrift = 0f;

    [Header("Crash Rotation")]
    public float pitchApproachSpeed = 2.5f;
    public float targetPitchAngle = 85f;
    public float pitchWobbleAmplitude = 18f;
    public float pitchWobbleFrequency = 6f;
    public float rollWobbleAmplitude = 12f;
    public float rollWobbleFrequency = 3f;
    public float yawWobbleAmplitude = 8f;
    public float yawWobbleFrequency = 4.2f;

    private float crashXSpeed;
    private float crashZSpeed;
    private float crashDownSpeed;
    private float crashPitch;
    private float crashTime;

    private float crashBaseYaw;
    private float crashBaseRoll;
    private float crashBasePitch;

    [Header("Movement")]
    public float cruiseSpeed = 10f;
    public float diveBoost = 6f;

    [Header("Dive/Climb")]
    public float diveDepth = 1.8f;
    public float diveDuration = 0.6f;
    public float climbDuration = 0.7f;

    [Header("Drop Trigger")]
    public float triggerDistance = 6.5f;
    public bool useHorizontalDistanceOnly = true;
    [Range(0f, 1f)] public float dropAtDiveProgress = 0.45f;

    [Header("Rotation (Z Axis)")]
    public float diveTiltAngle = 25f;
    public float rotationSmooth = 14f;

    [Header("Curves")]
    public AnimationCurve diveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve climbCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve diveSpeedCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve climbSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Bomb")]
    public GameObject bombPrefab;
    public Transform bombDropPoint;

    [Header("Explosion Rules")]
    [Tooltip("If true: only explode on env when state == Destroying. If false: explode on env anytime.")]
    [SerializeField] private bool requireDestroyingStateToExplode = true;

    private bool hasDropped;
    private bool hasExploded;

    private enum State { EnterCruise, Dive, Climb, Exit, Destroying }
    private State state = State.EnterCruise;

    private float baseY;
    private float diveT;
    private float climbT;

    private float targetZRotation;
    private float currentSpeed;

    // Physics
    private Rigidbody rb;

    // ✅ IMPORTANT: we must delay linearVelocity until AFTER kinematic is disabled and physics step runs
    private bool pendingCrashVelocityApply;

    protected override void Awake()
    {
        base.Awake();

        envLayer = LayerMask.NameToLayer(envLayerName);
        if (envLayer == -1)
            Debug.LogError($"JetEnemy: Layer '{envLayerName}' not found.");

        if (bombDropPoint == null) bombDropPoint = transform;

        targetZRotation = 0f;
        currentSpeed = cruiseSpeed;

        rb = GetComponent<Rigidbody>();
    }

    protected override void OnSpawnedInternal(EnemySpawnContext ctx)
    {
        state = State.EnterCruise;
        hasDropped = false;
        hasExploded = false;
        pendingCrashVelocityApply = false;

        targetZRotation = 0f;
        currentSpeed = cruiseSpeed;

        if (lifecycle != null) lifecycle.DisableDespawn = false;

        CancelInvoke(nameof(ExplodeSelf));

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    private void Update()
    {
        if (state == State.Destroying)
        {
            // if no RB, fallback to transform movement
            if (rb == null) DoDestroyingFall_Transform();
            return;
        }

        if (state == State.EnterCruise)
        {
            targetZRotation = 0f;
            currentSpeed = cruiseSpeed;

            if (player != null && ShouldTriggerAttack())
                StartDive();
        }
        else if (state == State.Dive) DoDive();
        else if (state == State.Climb) DoClimb();
        else if (state == State.Exit)
        {
            targetZRotation = 0f;
            currentSpeed = cruiseSpeed;
        }

        MoveForwardX();
        ApplyRotationZPreserveY();

        if (lifecycle != null) lifecycle.Tick();
    }

    private void FixedUpdate()
    {
        if (state != State.Destroying || rb == null) return;

        // ✅ Apply initial crash velocity ONE physics step later (avoids the kinematic warning + missed collisions)
        if (pendingCrashVelocityApply)
        {
            rb.linearVelocity = new Vector3(crashXSpeed, -crashDownSpeed, crashZSpeed);
            rb.angularVelocity = Vector3.zero;
            pendingCrashVelocityApply = false;
            return;
        }

        DoDestroyingFall_Rigidbody();
    }

    public override void TakeDamage(DamageInfo info)
    {
        if (state == State.Destroying) return;
        base.TakeDamage(info);
    }

    protected override void Die()
    {
        EnterDestroying();
    }

    private void EnterDestroying()
    {
        if (state == State.Destroying) return;

        state = State.Destroying;
        hasDropped = true;
        hasExploded = false;

        if (lifecycle != null) lifecycle.DisableDespawn = true;

        crashZSpeed = Mathf.Abs(crashInitialZSpeed);
        crashDownSpeed = Mathf.Abs(crashInitialDownSpeed);
        crashTime = 0f;

        crashBaseYaw = transform.eulerAngles.y;
        crashBaseRoll = NormalizeAngle(transform.eulerAngles.z);
        crashBasePitch = NormalizeAngle(transform.eulerAngles.x);
        crashPitch = crashBasePitch;

        float dirX = (transform.right.x >= 0f) ? 1f : -1f;
        float baseX = (crashInitialXSpeed != 0f) ? crashInitialXSpeed : currentSpeed;

        if (crashXRandomDrift > 0f)
            baseX += Random.Range(-crashXRandomDrift, crashXRandomDrift);

        crashXSpeed = Mathf.Abs(baseX) * dirX;

        if (smokeFirePrefab != null && smokeInstance == null)
            smokeInstance = Instantiate(smokeFirePrefab, transform.position, Quaternion.identity, transform);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // ❗ do NOT set rb.linearVelocity here (can still be treated as kinematic this frame)
            pendingCrashVelocityApply = true;
        }

        CancelInvoke(nameof(ExplodeSelf));
        Invoke(nameof(ExplodeSelf), Mathf.Max(0.05f, explodeFailSafeSeconds));
    }

    private void DoDestroyingFall_Transform()
    {
        crashTime += Time.deltaTime;

        float accelDir = (crashXSpeed >= 0f) ? 1f : -1f;
        crashXSpeed += accelDir * crashXAcceleration * Time.deltaTime;

        crashZSpeed += Mathf.Abs(crashZAcceleration) * Time.deltaTime;
        crashDownSpeed += Mathf.Abs(crashDownAcceleration) * Time.deltaTime;

        transform.position += new Vector3(
            crashXSpeed * Time.deltaTime,
            -crashDownSpeed * Time.deltaTime,
            crashZSpeed * Time.deltaTime
        );

        ApplyCrashRotation(Time.deltaTime);
    }

    private void DoDestroyingFall_Rigidbody()
    {
        crashTime += Time.fixedDeltaTime;

        float accelDir = (crashXSpeed >= 0f) ? 1f : -1f;
        crashXSpeed += accelDir * crashXAcceleration * Time.fixedDeltaTime;

        crashZSpeed += Mathf.Abs(crashZAcceleration) * Time.fixedDeltaTime;
        crashDownSpeed += Mathf.Abs(crashDownAcceleration) * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector3(crashXSpeed, -crashDownSpeed, crashZSpeed);

        ApplyCrashRotationRB();
    }

    private void ApplyCrashRotation(float dt)
    {
        crashPitch = Mathf.Lerp(crashPitch, targetPitchAngle, dt * Mathf.Max(0.01f, pitchApproachSpeed));

        float pitchWobble = Mathf.Sin(crashTime * pitchWobbleFrequency) * pitchWobbleAmplitude;
        float rollWobble = Mathf.Sin(crashTime * rollWobbleFrequency) * rollWobbleAmplitude;
        float yawWobble = (yawWobbleAmplitude <= 0f) ? 0f : Mathf.Sin(crashTime * yawWobbleFrequency) * yawWobbleAmplitude;

        float pitch = crashPitch + pitchWobble;
        float yaw = crashBaseYaw + yawWobble;
        float roll = crashBaseRoll + rollWobble;

        transform.rotation = Quaternion.Euler(pitch, yaw, roll);
    }

    private void ApplyCrashRotationRB()
    {
        crashPitch = Mathf.Lerp(crashPitch, targetPitchAngle, Time.fixedDeltaTime * Mathf.Max(0.01f, pitchApproachSpeed));

        float pitchWobble = Mathf.Sin(crashTime * pitchWobbleFrequency) * pitchWobbleAmplitude;
        float rollWobble = Mathf.Sin(crashTime * rollWobbleFrequency) * rollWobbleAmplitude;
        float yawWobble = (yawWobbleAmplitude <= 0f) ? 0f : Mathf.Sin(crashTime * yawWobbleFrequency) * yawWobbleAmplitude;

        float pitch = crashPitch + pitchWobble;
        float yaw = crashBaseYaw + yawWobble;
        float roll = crashBaseRoll + rollWobble;

        rb.MoveRotation(Quaternion.Euler(pitch, yaw, roll));
    }

    private void ExplodeSelf()
    {
        if (hasExploded) return;
        hasExploded = true;

        DieAfterExplosionAt(transform.position, explosionPrefab);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = (collision.contactCount > 0) ? collision.GetContact(0).point : transform.position;
        HandleEnvHit(collision.collider, hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        HandleEnvHit(other, hitPoint);
    }

    private void HandleEnvHit(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider == null) return;
        if (hasExploded) return;
        if (envLayer == -1) return;

        if (hitCollider.gameObject.layer != envLayer) return;
        if (requireDestroyingStateToExplode && state != State.Destroying) return;

        hasExploded = true;
        CancelInvoke(nameof(ExplodeSelf));

        DieAfterExplosionAt(hitPoint, explosionPrefab);
    }

    private void MoveForwardX()
    {
        float dir = (spawnSide == SpawnSide.Left) ? 1f : -1f;
        transform.position += new Vector3(dir * currentSpeed * Time.deltaTime, 0f, 0f);
    }

    private bool ShouldTriggerAttack()
    {
        Vector3 jetPos = transform.position;
        Vector3 pPos = player.position;

        float dist = useHorizontalDistanceOnly
            ? Mathf.Abs(jetPos.x - pPos.x)
            : Vector3.Distance(jetPos, pPos);

        return dist <= triggerDistance;
    }

    private void StartDive()
    {
        state = State.Dive;
        diveT = 0f;
        baseY = transform.position.y;

        targetZRotation = (spawnSide == SpawnSide.Left) ? -diveTiltAngle : diveTiltAngle;
    }

    private void DoDive()
    {
        diveT += Time.deltaTime;
        float t01 = Mathf.Clamp01(diveT / Mathf.Max(0.0001f, diveDuration));

        float y01 = diveCurve.Evaluate(t01);
        float y = Mathf.Lerp(baseY, baseY - diveDepth, y01);
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        float speed01 = diveSpeedCurve.Evaluate(t01);
        currentSpeed = cruiseSpeed + (diveBoost * speed01);

        float rot01 = diveCurve.Evaluate(t01);
        float signedTilt = (spawnSide == SpawnSide.Left) ? -diveTiltAngle : diveTiltAngle;
        targetZRotation = signedTilt * rot01;

        if (!hasDropped && t01 >= dropAtDiveProgress)
            DropBomb();

        if (t01 >= 1f)
        {
            state = State.Climb;
            climbT = 0f;
        }
    }

    private void DoClimb()
    {
        climbT += Time.deltaTime;
        float t01 = Mathf.Clamp01(climbT / Mathf.Max(0.0001f, climbDuration));

        float y01 = climbCurve.Evaluate(t01);
        float y = Mathf.Lerp(baseY - diveDepth, baseY, y01);
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        float speed01 = climbSpeedCurve.Evaluate(t01);
        currentSpeed = cruiseSpeed + (diveBoost * speed01);

        float signedTilt = (spawnSide == SpawnSide.Left) ? -diveTiltAngle : diveTiltAngle;
        targetZRotation = signedTilt * (1f - y01);

        if (t01 >= 1f)
        {
            targetZRotation = 0f;
            currentSpeed = cruiseSpeed;
            state = State.Exit;
        }
    }

    private void DropBomb()
    {
        if (bombPrefab == null) return;
        hasDropped = true;

        GameObject bombGO = Instantiate(bombPrefab, bombDropPoint.position, Quaternion.identity);
        Bomb bomb = bombGO.GetComponent<Bomb>();
        if (bomb != null) bomb.player = player;
    }

    private void ApplyRotationZPreserveY()
    {
        float currentZ = transform.eulerAngles.z;
        if (currentZ > 180f) currentZ -= 360f;

        float newZ = Mathf.Lerp(currentZ, targetZRotation, Time.deltaTime * rotationSmooth);

        float y = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, y, newZ);
    }

    private static float NormalizeAngle(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}

using System.Collections;
using UnityEngine;

public class TankEnemy : EnemyBase
{
    [Header("Environment Layer")]
    [SerializeField] private string envLayerName = "env";
    private int envLayer;

    [Header("Movement")]
    public float cruiseSpeed = 6f;
    public float engageDistance = 14f;

    [Header("Re-engage (Player moved away)")]
    [Tooltip("If player gets farther than this while tank is stopped/firing, tank will cruise again to get back in engageDistance.")]
    public float disengageDistance = 17f;

    [Header("Clamp Engage Position Inside Screen")]
    public bool clampEngageXToScreen = true;
    public float screenXMargin = 0.8f;

    [Header("Visual Root (child only)")]
    [Tooltip("Assign a CHILD transform here (tank body + cannon visuals). Shake moves only this.")]
    public Transform visualRoot;

    [Header("Wheels")]
    public Transform[] wheels;
    public float wheelRadius = 0.35f;
    public float wheelSpinMultiplier = 1f;

    [Header("Cannon")]
    public Transform cannon;         // rotating part
    public Transform muzzlePoint;    // where shell spawns
    public float cannonTurnSpeed = 8f;

    [Header("Arc / Triangle")]
    public float apexExtraHeight = 6f;
    public float topScreenMargin = 1.2f;

    [Header("Firing")]
    public float prepareToFireSeconds = 0.55f;
    public float fireIntervalSeconds = 2.5f;
    public float fireIntervalJitter = 0.35f;

    [Header("Shell (NO Rigidbody)")]
    public GameObject shellPrefab;         // uses TankShellProjectile (manual movement)
    public GameObject muzzleVfxPrefab;
    public float muzzleSpeed = 18f;
    public float gravity = 20f;

    [Header("Fire Shake")]
    public float shakeDuration = 0.12f;
    public float shakeMagnitude = 0.08f;

    [Header("Death")]
    public GameObject bigExplosionPrefab;

    [Header("Pause / Environment Sync")]
    public bool respondToEnvPause = true;

    [Tooltip("How much wheel-spin comes from environment movement when the tank is idle.")]
    public float envWheelSpinFactor = 1f;

    [Header("Tank-to-Tank Spacing")]
    public string enemyLayerName = "enemy";
    public float frontProbeDistance = 8f;
    public float frontProbeRadius = 1.2f;

    [Tooltip("EXTRA GAP between tanks. Collider sizes are added automatically now.")]
    public float minSpacingX = 6f;

    public float frontProbeUp = 1.2f;

    [Header("Facing")]
    [Tooltip("If your model's front is -X instead of +X, enable this.")]
    public bool modelFrontIsNegativeX = false;

    private enum State { EnterCruise, EngageStop, FiringLoop, Destroyed }
    private State state;

    private float fireTimer;
    private float nextFireDelay;

    private Vector3 baseVisualLocalPos;
    private bool isShaking;

    // env speed control (from event)
    private bool envPaused;
    private float envSpeedAbs;

    // spacing layers
    private int enemyLayer;
    private int enemyMask;

    private Rigidbody rb;

    // movement intent
    private bool wantMove;
    private float desiredMoveSpeed;

    protected override void Awake()
    {
        base.Awake();

        envLayer = LayerMask.NameToLayer(envLayerName);
        if (envLayer == -1)
            Debug.LogError($"TankEnemy: Layer '{envLayerName}' not found.");

        enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        if (enemyLayer == -1)
            Debug.LogError($"TankEnemy: Layer '{enemyLayerName}' not found.");
        enemyMask = (enemyLayer != -1) ? (1 << enemyLayer) : ~0;

        rb = GetComponent<Rigidbody>();

        if (visualRoot == null) visualRoot = transform; // you SHOULD assign a child
        baseVisualLocalPos = visualRoot.localPosition;

        if (muzzlePoint == null) muzzlePoint = cannon != null ? cannon : transform;

        ResetFireLoop();
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
        envPaused = evt.speed <= 0.0001f;
        envSpeedAbs = Mathf.Abs(evt.speed);
    }

    private void ResetFireLoop()
    {
        nextFireDelay = fireIntervalSeconds + Random.Range(-fireIntervalJitter, fireIntervalJitter);
        if (nextFireDelay < 0.1f) nextFireDelay = 0.1f;
    }

    // rotate ROOT so transform.right (+X) points to desired move direction
    private void ApplyFacingByX_Root()
    {
        Vector3 desiredRight = (spawnSide == SpawnSide.Left) ? Vector3.right : Vector3.left;
        if (modelFrontIsNegativeX) desiredRight = -desiredRight;
        transform.right = desiredRight;
    }

    protected override void OnSpawnedInternal(EnemySpawnContext ctx)
    {
        spawnSide = ctx.side;

        // rotate root once at spawn
        ApplyFacingByX_Root();

        state = State.EnterCruise;
        fireTimer = 0f;
        ResetFireLoop();

        if (visualRoot) visualRoot.localPosition = baseVisualLocalPos;
        isShaking = false;

        wantMove = true;
        desiredMoveSpeed = cruiseSpeed;
    }

    private void Update()
    {
        if (state == State.Destroyed) return;

        if (respondToEnvPause && envPaused)
        {
            wantMove = false;
            return;
        }

        UpdateWheelSpinBySpeed();

        switch (state)
        {
            case State.EnterCruise:
                {
                    wantMove = true;
                    desiredMoveSpeed = cruiseSpeed;

                    // ✅ Bug #1 fix: do NOT run front-tank stop logic until fully on screen
                    if (TryStopForFrontTankAndEngage())
                    {
                        RotateCannonUsingTriangle();
                        lifecycle?.Tick();
                        return;
                    }

                    // don't engage player until on screen
                    if (lifecycle != null && !lifecycle.HasEnteredScreen)
                        break;

                    if (ShouldEngagePlayer())
                    {
                        wantMove = false;
                        desiredMoveSpeed = 0f;

                        if (clampEngageXToScreen) ClampXInsideScreenNow();
                        if (lifecycle != null) lifecycle.DisableDespawn = true;

                        state = State.EngageStop;
                        fireTimer = -prepareToFireSeconds;
                    }
                    break;
                }

            case State.EngageStop:
                {
                    // ✅ Bug #2 fix: if player moved away while preparing, resume cruising to re-engage
                    if (ShouldResumeCruiseForPlayerDistance())
                    {
                        ResumeCruise();
                        break;
                    }

                    wantMove = false;
                    desiredMoveSpeed = 0f;

                    RotateCannonUsingTriangle();
                    fireTimer += Time.deltaTime;

                    if (fireTimer >= 0f)
                    {
                        state = State.FiringLoop;
                        fireTimer = 0f;
                        ResetFireLoop();
                    }
                    break;
                }

            case State.FiringLoop:
                {
                    // ✅ Bug #2 fix: if player moved away, chase again (get close → stop → fire)
                    if (ShouldResumeCruiseForPlayerDistance())
                    {
                        ResumeCruise();
                        break;
                    }

                    wantMove = false;
                    desiredMoveSpeed = 0f;

                    RotateCannonUsingTriangle();
                    fireTimer += Time.deltaTime;

                    if (fireTimer >= nextFireDelay)
                    {
                        FireOneShotBallistic();
                        fireTimer = 0f;
                        ResetFireLoop();
                    }
                    break;
                }
        }

        lifecycle?.Tick();
    }

    private void ResumeCruise()
    {
        // Go back to cruise so we can approach player again
        state = State.EnterCruise;
        wantMove = true;
        desiredMoveSpeed = cruiseSpeed;

        // Optional: if you want tanks to be allowed to despawn again when re-chasing, uncomment:
        // if (lifecycle != null) lifecycle.DisableDespawn = false;
    }

    private bool ShouldResumeCruiseForPlayerDistance()
    {
        if (player == null) return false;

        float myX = (rb != null) ? rb.position.x : transform.position.x;
        float dx = Mathf.Abs(myX - player.position.x);

        // Only start re-chasing once player is clearly away (hysteresis to avoid jitter)
        return dx > Mathf.Max(engageDistance + 0.5f, disengageDistance);
    }

    private void FixedUpdate()
    {
        if (state == State.Destroyed) return;
        if (respondToEnvPause && envPaused) return;
        if (rb == null) return;

        float speed = wantMove ? desiredMoveSpeed : 0f;

        // move along ROOT +X direction (transform.right)
        Vector3 moveDir = transform.right;

        Vector3 v = rb.linearVelocity;
        v.x = moveDir.x * speed;
        v.z = 0f;
        rb.linearVelocity = v;
    }

    private void UpdateWheelSpinBySpeed()
    {
        if (wheels == null || wheels.Length == 0) return;
        if (envSpeedAbs <= 0.0001f) return;

        float tankMoveSpeed = (state == State.EnterCruise && wantMove) ? cruiseSpeed : 0f;
        float effectiveSpeed = (envSpeedAbs * envWheelSpinFactor) + tankMoveSpeed;

        float dir = Mathf.Sign(transform.right.x);
        if (dir == 0f) dir = 1f;

        float dt = Time.deltaTime;
        float r = Mathf.Max(0.001f, wheelRadius);
        float angleDeg = ((effectiveSpeed * dir) * dt / r) * Mathf.Rad2Deg * wheelSpinMultiplier;

        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i]) wheels[i].Rotate(Vector3.forward, angleDeg, Space.Self);
    }

    private bool ShouldEngagePlayer()
    {
        if (player == null) return false;

        float myX = (rb != null) ? rb.position.x : transform.position.x;
        return Mathf.Abs(myX - player.position.x) <= engageDistance;
    }

    private bool TryStopForFrontTankAndEngage()
    {
        // ✅ Bug #1 fix: only do spacing checks once we fully entered screen
        if (lifecycle != null && !lifecycle.HasEnteredScreen)
            return false;

        if (enemyLayer == -1) return false;

        // use root-facing direction for "front"
        Vector3 castDir = transform.right.x >= 0f ? Vector3.right : Vector3.left;

        Vector3 myPos = (rb != null) ? rb.position : transform.position;
        Vector3 origin = myPos + Vector3.up * frontProbeUp;

        if (!Physics.SphereCast(origin, frontProbeRadius, castDir, out RaycastHit hit,
                frontProbeDistance, enemyMask, QueryTriggerInteraction.Collide))
            return false;

        // ignore self
        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return false;

        // NOTE: We intentionally only react to OTHER TankEnemy here.
        // This already prevents DroneEnemy / other enemy types from stopping the tank.
        TankEnemy other = hit.collider ? hit.collider.GetComponentInParent<TankEnemy>() : null;
        if (other == null || other == this) return false;

        float otherX = (other.rb != null) ? other.rb.position.x : other.transform.position.x;
        float myX = myPos.x;

        // must be actually in front (based on castDir)
        if (castDir == Vector3.right && otherX <= myX) return false;
        if (castDir == Vector3.left && otherX >= myX) return false;

        Collider myCol = GetComponentInChildren<Collider>();
        Collider otherCol = other.GetComponentInChildren<Collider>();
        if (myCol == null || otherCol == null) return false;

        float requiredSpacing = myCol.bounds.extents.x + otherCol.bounds.extents.x + minSpacingX;
        float dx = Mathf.Abs(otherX - myX);

        if (dx <= requiredSpacing)
        {
            wantMove = false;
            desiredMoveSpeed = 0f;

            // ✅ As soon as we are on screen and too close to the front tank:
            // enter firing loop immediately (your desired behavior).
            if (lifecycle != null && lifecycle.HasEnteredScreen)
            {
                if (clampEngageXToScreen) ClampXInsideScreenNow();
                lifecycle.DisableDespawn = true;

                state = State.FiringLoop;
                fireTimer = 0f;
                ResetFireLoop();
            }
            return true;
        }

        return false;
    }

    private Vector3 ComputeApexPointC(Vector3 A, Vector3 B)
    {
        Camera c = cam != null ? cam : Camera.main;

        float apexX = (A.x + B.x) * 0.5f;
        float desiredApexY = Mathf.Max(A.y, B.y) + apexExtraHeight;

        if (c == null)
            return new Vector3(apexX, desiredApexY, A.z);

        float zPlane = A.z;
        Vector3 topWorld = c.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(c.transform.position.z - zPlane)));
        float maxApexY = topWorld.y - topScreenMargin;

        float apexY = Mathf.Min(desiredApexY, maxApexY);
        apexY = Mathf.Max(apexY, Mathf.Max(A.y, B.y) + 0.5f);

        return new Vector3(apexX, apexY, A.z);
    }

    private void RotateCannonUsingTriangle()
    {
        if (!cannon || !muzzlePoint || !player) return;

        Vector3 A = muzzlePoint.position;
        Vector3 B = player.position;
        Vector3 C = ComputeApexPointC(A, B);

        Vector3 dir = (C - A);
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion target = Quaternion.Euler(0f, 0f, angleDeg);

        cannon.rotation = Quaternion.Slerp(cannon.rotation, target, Time.deltaTime * cannonTurnSpeed);
    }

    private void FireOneShotBallistic()
    {
        if (!shellPrefab || !muzzlePoint || !player) return;

        if (muzzleVfxPrefab)
            Instantiate(muzzleVfxPrefab, muzzlePoint.position, muzzlePoint.rotation);

        Vector3 A = muzzlePoint.position;
        Vector3 B = player.position;
        Vector3 C = ComputeApexPointC(A, B);

        float dx = (B.x - A.x);
        float absDx = Mathf.Max(0.2f, Mathf.Abs(dx));

        float t = Mathf.Max(0.25f, absDx / Mathf.Max(0.1f, muzzleSpeed));
        float g = Mathf.Max(0.01f, gravity);

        float vyIdeal = (B.y - A.y + 0.5f * g * t * t) / t;
        float apexYIdeal = A.y + (vyIdeal * vyIdeal) / (2f * g);

        float vy = vyIdeal;
        if (apexYIdeal > C.y)
        {
            float apexDelta = Mathf.Max(0.01f, C.y - A.y);
            vy = Mathf.Sqrt(2f * g * apexDelta);
        }

        float vx = dx / t;
        Vector3 v0 = new Vector3(vx, vy, 0f);

        GameObject shellGO = Instantiate(shellPrefab, A, Quaternion.identity);
        var shell = shellGO.GetComponent<TankShellProjectile>();
        if (shell != null)
            shell.InitManual(player, v0, g, envLayerName);

        if (!isShaking)
            StartCoroutine(Shake());
    }

    private IEnumerator Shake()
    {
        if (!visualRoot) yield break;

        isShaking = true;
        float t = 0f;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / Mathf.Max(0.0001f, shakeDuration));

            visualRoot.localPosition = baseVisualLocalPos +
                new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeMagnitude * k;

            yield return null;
        }

        visualRoot.localPosition = baseVisualLocalPos;
        isShaking = false;
    }

    private void ClampXInsideScreenNow()
    {
        Camera c = cam != null ? cam : Camera.main;
        if (!c) return;

        Vector3 pos = (rb != null) ? rb.position : transform.position;

        float z = pos.z;
        Plane p = new Plane(Vector3.forward, new Vector3(0, 0, z));

        Ray l = c.ViewportPointToRay(new Vector3(0, 0.5f));
        Ray r = c.ViewportPointToRay(new Vector3(1, 0.5f));

        p.Raycast(l, out float el);
        p.Raycast(r, out float er);

        float minX = l.GetPoint(el).x + screenXMargin;
        float maxX = r.GetPoint(er).x - screenXMargin;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (rb != null) rb.MovePosition(pos);
        else transform.position = pos;
    }

    protected override void Die()
    {
        state = State.Destroyed;
        if (lifecycle != null) lifecycle.DisableDespawn = true;

        DieAfterExplosionAt(transform.position, bigExplosionPrefab);
    }
}

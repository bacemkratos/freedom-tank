using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TankHorizontalMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 20f;

    [Header("Collision Blocking")]
    [Tooltip("Layer name used by enemies (e.g., 'enemy').")]
    [SerializeField] private string enemyLayerName = "enemy";
    [Tooltip("Small tolerance to avoid jitter when positions are almost equal.")]
    [SerializeField] private float blockDeadZone = 0.01f;

    [Header("Camera & Bounds")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField, Range(0f, 0.4f)]
    private float horizontalViewportPadding = 0.05f;

    private float _leftLimit;
    private float _rightLimit;

    private bool _canMove = false;

    private Rigidbody _rb;
    private float _input;

    private int _enemyLayer;

    // Directional block flags (set by collisions)
    private bool _blockLeft;
    private bool _blockRight;

    private void OnEnable()
    {
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelEndEvent>(OnLevelEnd);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelEndEvent>(OnLevelEnd);
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null)
        {
            Debug.LogError("[TankHorizontalMovement] No camera assigned and no Camera.main found.");
            enabled = false;
            return;
        }

        _enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        if (_enemyLayer < 0)
        {
            Debug.LogError($"[TankHorizontalMovement] Enemy layer '{enemyLayerName}' not found. Check your Layer settings.");
            enabled = false;
            return;
        }

        CacheBoundsFromCamera();
    }

    private void Update()
    {
        if (!_canMove)
        {
            _input = 0f;
            return;
        }

        _input = Input.GetAxisRaw("Horizontal"); // -1, 0, 1
        BroadcastTankSpeed(_input);
    }

    private void FixedUpdate()
    {
        if (!_canMove) return;

        // Apply directional blocking
        float input = _input;
        if (input > 0f && _blockRight) input = 0f; // enemy on right -> block right movement
        if (input < 0f && _blockLeft) input = 0f; // enemy on left  -> block left movement

        float delta = input * moveSpeed * Time.fixedDeltaTime;

        Vector3 pos = _rb.position;
        pos.x = Mathf.Clamp(pos.x + delta, _leftLimit, _rightLimit);

        _rb.MovePosition(pos);

        // Clear each physics step; collisions will re-set if still touching
        _blockLeft = false;
        _blockRight = false;

        // If we were pressing but got blocked, stop wheels
        if (Mathf.Approximately(input, 0f) && !Mathf.Approximately(_input, 0f))
        {
            EventBus.Raise(new TankSpeedChangedEvent(0f));
        }
    }

    private void OnCollisionStay(Collision c)
    {
        if (c.gameObject.layer != _enemyLayer) return;

        float dx = c.transform.position.x - transform.position.x;
        if (dx > blockDeadZone) _blockRight = true;
        else if (dx < -blockDeadZone) _blockLeft = true;
    }

    private void OnCollisionExit(Collision c)
    {
        if (c.gameObject.layer != _enemyLayer) return;
        _blockLeft = false;
        _blockRight = false;
    }

    private void CacheBoundsFromCamera()
    {
        Vector3 camPos = gameplayCamera.transform.position;
        Vector3 toTank = transform.position - camPos;
        float depth = Vector3.Dot(toTank, gameplayCamera.transform.forward);

        Vector3 leftWorld = gameplayCamera.ViewportToWorldPoint(
            new Vector3(horizontalViewportPadding, 0.5f, depth));
        Vector3 rightWorld = gameplayCamera.ViewportToWorldPoint(
            new Vector3(1f - horizontalViewportPadding, 0.5f, depth));

        _leftLimit = Mathf.Min(leftWorld.x, rightWorld.x);
        _rightLimit = Mathf.Max(leftWorld.x, rightWorld.x);
    }

    private void BroadcastTankSpeed(float input)
    {
        float tankSpeed = input * moveSpeed;
        EventBus.Raise(new TankSpeedChangedEvent(tankSpeed));
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        _canMove = true;
        CacheBoundsFromCamera();
        EventBus.Raise(new TankSpeedChangedEvent(0f));
    }

    private void OnLevelEnd(LevelEndEvent e)
    {
        _canMove = false;
        EventBus.Raise(new TankSpeedChangedEvent(0f));
    }
}

using System.Collections.Generic;
using UnityEngine;

public class LandTileGridScroller : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private Transform leftAnchor;
    [SerializeField] private Transform rightAnchor;

    [Header("Direction Fix")]
    [SerializeField] private bool invertAxisDirection = false;

    [Header("Land Origin Offset (Margin)")]
    [Tooltip("Offsets the whole land grid relative to the anchors.\nX = along scroll axis, Y = height, Z = depth.")]
    [SerializeField] private Vector3 gridOffset = new Vector3(0f, 0f, 10f);

    [Header("Tile Size (Auto if 0)")]
    [SerializeField] private float tileLength = 0f;
    [SerializeField] private float tileDepth = 0f;

    [Header("Grid")]
    [SerializeField] private int rowCount = 6;

    [Tooltip("Distance between rows in depth (Z). If 0, uses tileDepth.")]
    [SerializeField] private float rowDepthSpacing = 0f;

    [Tooltip("Extra Y added per row (stairs effect).")]
    [SerializeField] private float rowStepHeight = 0.25f;

    [Tooltip("Horizontal gap/overlap between tiles. Negative removes gaps.")]
    [SerializeField] private float spacingAdjustment = 0f;

    [SerializeField] private int extraColumns = 2;

    [Header("Movement")]
    [SerializeField] private float scrollSpeed = 10f;

    [Tooltip("If true, tiles move from right to left (classic).")]
    [SerializeField] private bool moveLeft = true;

    [Header("Run Control (Events)")]
    [SerializeField] private bool startRunning = true;

    private Vector3 _axisDir;
    private Vector3 _leftPos;
    private float _span;

    private readonly List<Vector3> _rowBases = new();
    private readonly List<List<Transform>> _rows = new();

    private bool _isRunning;

    private void Awake()
    {
        if (!tilePrefab || !leftAnchor || !rightAnchor)
        {
            Debug.LogError("[LandTileGridScroller] Assign tilePrefab + leftAnchor + rightAnchor.");
            enabled = false;
            return;
        }

        AutoCalcTileSize();
        InitAxis();

        _isRunning = startRunning;
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
        _isRunning = evt.speed > 0.0001f;
        scrollSpeed = Mathf.Abs(evt.speed);
    }

    private void Start() => SpawnGrid();

    private void Update()
    {
        if (!_isRunning) return;

        MoveAll();
        RecycleAllRows();
    }

    // -------- Public controls --------
    public void StartScroll() => _isRunning = true;
    public void StopScroll() => _isRunning = false;
    public void SetSpeed(float newSpeed) => scrollSpeed = newSpeed;
    public void SetDirection(bool toLeft) => moveLeft = toLeft;

    private void AutoCalcTileSize()
    {
        var rend = tilePrefab.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            if (tileLength <= 0f) tileLength = rend.bounds.size.x;
            if (tileDepth <= 0f) tileDepth = rend.bounds.size.z;
        }
        else
        {
            if (tileLength <= 0f) tileLength = 20f;
            if (tileDepth <= 0f) tileDepth = 20f;
            Debug.LogWarning("[LandTileGridScroller] No renderer found, using fallback 20x20.");
        }

        if (rowDepthSpacing <= 0f)
            rowDepthSpacing = tileDepth;
    }

    private void InitAxis()
    {
        _leftPos = leftAnchor.position + gridOffset;
        Vector3 rightPos = rightAnchor.position + gridOffset;

        _span = Vector3.Distance(_leftPos, rightPos);
        if (_span < 0.01f) _span = tileLength;

        _axisDir = (rightPos - _leftPos).normalized;

        if (invertAxisDirection)
            _axisDir = -_axisDir;
    }

    private void SpawnGrid()
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int i = 0; i < _rows[r].Count; i++)
                if (_rows[r][i]) Destroy(_rows[r][i].gameObject);

        _rows.Clear();
        _rowBases.Clear();

        float stepX = tileLength + spacingAdjustment;
        int columns = Mathf.CeilToInt(_span / stepX) + extraColumns;
        if (columns < 3) columns = 3;

        float startT = -stepX;

        for (int r = 0; r < rowCount; r++)
        {
            Vector3 rowBase = _leftPos
                            + Vector3.forward * (r * rowDepthSpacing)
                            + Vector3.up * (r * rowStepHeight);

            _rowBases.Add(rowBase);

            var rowList = new List<Transform>(columns);

            for (int c = 0; c < columns; c++)
            {
                float t = startT + c * stepX;
                Vector3 pos = rowBase + _axisDir * t;

                GameObject seg = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
                rowList.Add(seg.transform);
            }

            _rows.Add(rowList);
        }
    }

    private Vector3 ScrollDirWorld()
    {
        return moveLeft ? -_axisDir : _axisDir;
    }

    private void MoveAll()
    {
        Vector3 scrollDir = ScrollDirWorld();
        float delta = scrollSpeed * Time.deltaTime;

        foreach (var row in _rows)
            foreach (var t in row)
                t.Translate(scrollDir * delta, Space.World);
    }

    private void RecycleAllRows()
    {
        float stepX = tileLength + spacingAdjustment;
        Vector3 scrollDir = ScrollDirWorld();

        float sign = Mathf.Sign(Vector3.Dot(scrollDir, _axisDir));

        float minVisibleT = -stepX * 1.2f;
        float maxVisibleT = _span + stepX * 1.2f;

        for (int r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];

            float minT = float.MaxValue;
            float maxT = float.MinValue;

            foreach (var seg in row)
            {
                float t = Vector3.Dot(seg.position - _leftPos, _axisDir);
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }

            Vector3 baseRow = _rowBases[r];

            foreach (var seg in row)
            {
                float t = Vector3.Dot(seg.position - _leftPos, _axisDir);

                if (sign > 0f && t > maxVisibleT)
                {
                    float newT = minT - stepX;
                    seg.position = baseRow + _axisDir * newT;
                    minT = newT;
                }
                else if (sign < 0f && t < minVisibleT)
                {
                    float newT = maxT + stepX;
                    seg.position = baseRow + _axisDir * newT;
                    maxT = newT;
                }
            }
        }
    }
}

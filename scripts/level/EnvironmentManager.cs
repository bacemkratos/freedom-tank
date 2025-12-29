using System.Collections.Generic;
using UnityEngine;

public class RoadEnvironmentManager : MonoBehaviour
{
    [Header("Alignment")]
    [SerializeField] private float spacingAdjustment = 0f;

    [Header("Setup")]
    [SerializeField] private GameObject roadSegmentPrefab;

    [Tooltip("Left point of the visible road span (slightly off-screen to the left).")]
    [SerializeField] private Transform leftAnchor;

    [Tooltip("Right point of the visible road span (slightly off-screen to the right).")]
    [SerializeField] private Transform rightAnchor;

    [Header("Road Settings")]
    [SerializeField] private float segmentLength = 0f;   // 0 = auto from prefab
    [SerializeField] private int extraSegments = 2;      // extra tiles beyond span

    [Header("Movement")]
    [SerializeField] private float scrollSpeed = 10f;    // units per second

    [Header("Run Control (Events)")]
    [SerializeField] private bool startRunning = true;

    private readonly List<Transform> _segments = new();

    private Vector3 _dir;        // direction from left to right
    private Vector3 _leftPos;    // cached leftAnchor position
    private float _span;         // distance between left and right

    private bool _isRunning;

    private void Awake()
    {
        if (roadSegmentPrefab == null)
        {
            Debug.LogError("[RoadEnvironmentManager] Road prefab not assigned.");
            enabled = false;
            return;
        }

        if (leftAnchor == null || rightAnchor == null)
        {
            Debug.LogError("[RoadEnvironmentManager] Left/Right anchors must be assigned.");
            enabled = false;
            return;
        }

        if (segmentLength <= 0f)
            AutoCalculateSegmentLength();

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

    private void Start()
    {
        SpawnInitialSegments();
    }

    private void Update()
    {
        if (!_isRunning) return;

        MoveSegments();
        RecycleSegments();
    }

    private void AutoCalculateSegmentLength()
    {
        var rend = roadSegmentPrefab.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            segmentLength = rend.bounds.size.x;
        }
        else
        {
            segmentLength = 20f;
            Debug.LogWarning("[RoadEnvironmentManager] No renderer on prefab, using fallback length 20.");
        }
    }

    private void InitAxis()
    {
        _leftPos = leftAnchor.position;
        Vector3 rightPos = rightAnchor.position;

        _span = Vector3.Distance(_leftPos, rightPos);
        if (_span < 0.01f)
        {
            _span = segmentLength;
        }

        _dir = (rightPos - _leftPos).normalized;
    }

    private void SpawnInitialSegments()
    {
        _segments.Clear();

        float step = segmentLength + spacingAdjustment;

        int needed = Mathf.CeilToInt(_span / step) + extraSegments;
        if (needed < 3) needed = 3;

        float startT = -step;
        for (int i = 0; i < needed; i++)
        {
            float t = startT + i * step;
            Vector3 pos = _leftPos + _dir * t;

            pos.y = _leftPos.y;
            pos.z = _leftPos.z;

            GameObject seg = Instantiate(roadSegmentPrefab, pos, Quaternion.identity, transform);
            _segments.Add(seg.transform);
        }

        Debug.Log($"[RoadEnvironmentManager] Spawned {needed} road segments. segmentLength={segmentLength}, step={step}, span={_span}");
    }

    private void MoveSegments()
    {
        Vector3 moveDir = _dir;
        float delta = scrollSpeed * Time.deltaTime;

        foreach (var seg in _segments)
            seg.Translate(moveDir * delta, Space.World);
    }

    private void RecycleSegments()
    {
        float step = segmentLength + spacingAdjustment;

        float rightLimit = _span + step * 1.2f;
        float minT = float.MaxValue;

        foreach (var seg in _segments)
        {
            float t = Vector3.Dot(seg.position - _leftPos, _dir);
            if (t < minT)
                minT = t;
        }

        foreach (var seg in _segments)
        {
            float t = Vector3.Dot(seg.position - _leftPos, _dir);

            if (t > rightLimit)
            {
                float newT = minT - step;
                Vector3 newPos = _leftPos + _dir * newT;
                newPos.y = _leftPos.y;
                newPos.z = _leftPos.z;

                seg.position = newPos;
                minT = newT;
            }
        }
    }

    // Optional public controls
    public void StartScroll() => _isRunning = true;
    public void StopScroll() => _isRunning = false;
    public void SetSpeed(float s) => scrollSpeed = s;
}

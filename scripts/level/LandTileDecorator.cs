using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LandTileDecorator : MonoBehaviour
{
    [Header("Prefabs To Spawn")]
    [SerializeField] private List<GameObject> spawnPrefabs = new();

    [Header("Spawn Count")]
    [Min(0)][SerializeField] private int minSpawn = 0;
    [Min(0)][SerializeField] private int maxSpawn = 3;

    [Header("Surface / Bounds")]
    [Tooltip("Optional: assign the collider that represents the land top. If empty, will auto-find on this object/children.")]
    [SerializeField] private Collider landCollider;

    [Tooltip("Keeps spawns away from tile edges (world units).")]
    [SerializeField] private float edgeMargin = 0.5f;

    [Header("Spacing Preset (1=Near ... 5=Far)")]
    [Range(1, 5)]
    [SerializeField] private int spacingLevel = 3;

    [Tooltip("Min distance between objects for each spacing level (1..5).")]
    [SerializeField] private float[] spacingLevels = new float[5] { 0.8f, 1.4f, 2.0f, 2.8f, 3.8f };

    [Header("Rotation / Axis Fix")]
    [Tooltip("If your prefabs are Blender Z-up, enable this to treat local Z (transform.forward) as the 'up' axis.")]
    [SerializeField] private bool useZAsUp = true;

    [SerializeField] private bool randomYaw = true;

    [Header("Placement Attempts")]
    [Tooltip("More attempts helps when spacing is large or the tile is small.")]
    [SerializeField] private int maxAttemptsPerObject = 25;

    [Header("Parenting")]
    [SerializeField] private Transform spawnedParent;

    private readonly List<Vector3> _placed = new();

    private void Awake()
    {
        if (landCollider == null)
            landCollider = GetComponentInChildren<Collider>();

        if (spawnedParent == null)
            spawnedParent = transform;
    }

    private void Start()
    {
        Decorate();
    }

    public void Decorate()
    {
        if (spawnPrefabs == null || spawnPrefabs.Count == 0) return;

        if (landCollider == null)
        {
            Debug.LogWarning($"[LandTileDecorator] No collider found on {name}. Cannot place objects on surface.");
            return;
        }

        if (maxSpawn < minSpawn) maxSpawn = minSpawn;

        int count = Random.Range(minSpawn, maxSpawn + 1);
        if (count <= 0) return;

        _placed.Clear();

        float minDist = GetMinDistance();
        Bounds b = landCollider.bounds;

        float minX = b.min.x + edgeMargin;
        float maxX = b.max.x - edgeMargin;
        float minZ = b.min.z + edgeMargin;
        float maxZ = b.max.z - edgeMargin;

        if (minX >= maxX || minZ >= maxZ)
        {
            Debug.LogWarning($"[LandTileDecorator] edgeMargin too large for tile {name}.");
            return;
        }

        for (int i = 0; i < count; i++)
            TryPlaceOne(minX, maxX, minZ, maxZ, b.max.y + 10f, minDist);
    }

    private float GetMinDistance()
    {
        int idx = Mathf.Clamp(spacingLevel - 1, 0, spacingLevels.Length - 1);
        return Mathf.Max(0.01f, spacingLevels[idx]);
    }

    private bool TryPlaceOne(float minX, float maxX, float minZ, float maxZ, float rayStartY, float minDist)
    {
        for (int attempt = 0; attempt < maxAttemptsPerObject; attempt++)
        {
            float x = Random.Range(minX, maxX);
            float z = Random.Range(minZ, maxZ);

            Vector3 rayStart = new Vector3(x, rayStartY, z);
            Ray ray = new Ray(rayStart, Vector3.down);

            if (!landCollider.Raycast(ray, out RaycastHit hit, 999f))
                continue;

            if (hit.collider != landCollider)
                continue;

            Vector3 pos = hit.point;

            if (!IsFarEnough(pos, minDist))
                continue;

            GameObject prefab = spawnPrefabs[Random.Range(0, spawnPrefabs.Count)];
            if (prefab == null) continue;

            GameObject go = Instantiate(prefab, pos, prefab.transform.rotation, spawnedParent);

            // ---- Axis handling ----
            // If prefab is Z-up: align its FORWARD (Z) to the ground normal.
            // Else: align its UP (Y) to the ground normal.
            Vector3 prefabUpAxis = useZAsUp ? go.transform.forward : go.transform.up;

            go.transform.rotation =
                Quaternion.FromToRotation(prefabUpAxis, hit.normal) * go.transform.rotation;

            // Random spin around the surface normal (works after alignment)
            if (randomYaw)
            {
                float yaw = Random.Range(0f, 360f);
                go.transform.Rotate(hit.normal, yaw, Space.World);
            }

            SnapToSurface(go, hit);

            _placed.Add(pos);
            return true;
        }

        return false;
    }

    private bool IsFarEnough(Vector3 candidate, float minDist)
    {
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < _placed.Count; i++)
        {
            Vector3 a = _placed[i];
            float dx = candidate.x - a.x;
            float dz = candidate.z - a.z;
            float d2 = dx * dx + dz * dz;

            if (d2 < minDistSqr)
                return false;
        }

        return true;
    }

    private void SnapToSurface(GameObject go, RaycastHit surfaceHit)
    {
        var r = go.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Bounds rb = r.bounds;
        float bottomY = rb.min.y;

        float delta = surfaceHit.point.y - bottomY;
        go.transform.position += new Vector3(0f, delta, 0f);
    }
}

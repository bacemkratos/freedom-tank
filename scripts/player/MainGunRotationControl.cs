using UnityEngine;

public class MainGunRotationControl : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform pivot; // the transform that rotates (your pivot object)

    [Header("Rotation")]
    [SerializeField] private float angleOffsetDeg = 0f; // try -90 / 90 / 180 if needed
    [SerializeField] private float minAngleDeg = -180f;
    [SerializeField] private float maxAngleDeg = 180f;

    [Header("Smoothing")]
    [SerializeField] private bool smooth = true;
    [SerializeField] private float smoothSpeed = 20f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Transform space; // we compute angle in this space (parent space)

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!pivot) pivot = transform;

        // Use parent space for correct local aiming
        space = pivot.parent ? pivot.parent : pivot;
    }

    private void Update()
    {
        if (!cam) return;

        // Ray from mouse
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        // IMPORTANT:
        // We want to rotate around pivot's local Z axis, so the aiming plane normal is pivot's local Z in world space.
        Vector3 planeNormal = pivot.TransformDirection(Vector3.forward); // local +Z
        Plane aimPlane = new Plane(planeNormal, pivot.position);

        if (!aimPlane.Raycast(ray, out float enter))
            return;

        Vector3 hitWorld = ray.GetPoint(enter);

        // Convert points into "space" (parent) local coordinates so atan2 is stable
        Vector3 pivotLocal = space.InverseTransformPoint(pivot.position);
        Vector3 hitLocal = space.InverseTransformPoint(hitWorld);

        Vector2 dir = (Vector2)(hitLocal - pivotLocal);
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffsetDeg;
        angle = ClampAngle(angle, minAngleDeg, maxAngleDeg);

        Quaternion targetLocalRot = Quaternion.Euler(0f, 0f, angle);

        if (smooth)
            pivot.localRotation = Quaternion.Slerp(pivot.localRotation, targetLocalRot, Time.deltaTime * smoothSpeed);
        else
            pivot.localRotation = targetLocalRot;

        if (drawDebug)
        {
            Debug.DrawLine(pivot.position, hitWorld, Color.yellow);
            Debug.DrawRay(pivot.position, pivot.right * 2f, Color.red); // where +X aims
        }
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        angle = NormalizeAngle(angle);
        min = NormalizeAngle(min);
        max = NormalizeAngle(max);

        if (min > max)
        {
            if (angle < min && angle > max)
            {
                float dMin = Mathf.DeltaAngle(angle, min);
                float dMax = Mathf.DeltaAngle(angle, max);
                angle = Mathf.Abs(dMin) < Mathf.Abs(dMax) ? min : max;
            }
            return angle;
        }

        return Mathf.Clamp(angle, min, max);
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }
}

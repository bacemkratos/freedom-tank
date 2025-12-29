using UnityEngine;

public static class SpawnPointResolver
{
    // How far outside the camera view we spawn
    private const float margin = 6f;

    // Random vertical range INSIDE the view (with padding) when spawning left/right
    private const float yPadding = 1.5f;

    // Random horizontal range INSIDE the view (with padding) when spawning top
    private const float xPadding = 2f;

    /// <summary>
    /// Returns a spawn position just outside the camera view.
    /// Assumes your gameplay is side-view (X left/right, Y up/down) and enemies fly in screen plane.
    /// </summary>
    public static Vector3 GetPosition(SpawnSide side, float zPlane = 0f)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return new Vector3(0, 0, zPlane);

        // We need bounds at a specific Z plane (where enemies live).
        // For perspective camera, we compute distance from camera to that plane.
        float distance = Mathf.Abs(zPlane - cam.transform.position.z);

        // Viewport corners at that distance
        Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance));

        float leftX = bottomLeft.x;
        float rightX = topRight.x;
        float bottomY = bottomLeft.y;
        float topY = topRight.y;

        // Keep spawns within vertical/horizontal safe ranges
        float y = Random.Range(bottomY + yPadding, topY - yPadding);
        float x = Random.Range(leftX + xPadding, rightX - xPadding);

        // Random side support
        if (side == SpawnSide.Random)
            side = (Random.value < 0.5f) ? SpawnSide.Left : SpawnSide.Right;

        return side switch
        {
            SpawnSide.Left => new Vector3(leftX - margin, y, zPlane),
            SpawnSide.Right => new Vector3(rightX + margin, y, zPlane),

            // If you later add Top spawns:
            // SpawnSide.Top => new Vector3(x, topY + margin, zPlane),

            _ => new Vector3(leftX - margin, y, zPlane),
        };
    }


}

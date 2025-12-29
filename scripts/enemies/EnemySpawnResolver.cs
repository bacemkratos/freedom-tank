using UnityEngine;

public static class EnemySpawnResolver
{
    public static Vector3 ResolveSpawn(EnemySpawnData data, Camera cam, Transform player, SpawnSide side)
    {
     

        if (data.mode == SpawnMode.FixedWorld)
            return ResolveFixed(data, player);

        return ResolveOffscreenX(data, cam, player, side);
    }

    private static Vector3 ResolveFixed(EnemySpawnData data, Transform player)
    {
        Vector3 p = data.fixedWorldPosition;

        if (data.fixedUsePlayerZ)
            p.z = player.position.z;

        // Apply z offset/random if you want consistency across both modes
        float z = player.position.z + data.zOffset;
        if (data.zRandomRange > 0f)
            z += Random.Range(-data.zRandomRange, data.zRandomRange);

        p.z = z;
        return p;
    }

    // ✅ The correct offscreen spawn for your game: offscreen in X, on player's Z plane.
    private static Vector3 ResolveOffscreenX(EnemySpawnData data, Camera cam, Transform player, SpawnSide side)
    {
        // target Z plane: same as player + optional offsets/random
        float z = player.position.z + data.zOffset;
        if (data.zRandomRange > 0f)
            z += Random.Range(-data.zRandomRange, data.zRandomRange);

        // Offscreen X in viewport coordinates
        float vx = (side == SpawnSide.Left) ? -0.1f : 1.1f;

        // Random Y band (viewport)
        float vy = Random.Range(data.minYViewport, data.maxYViewport);

        // Convert viewport point to world point ON the Z plane using ray-plane intersection
        Vector3 world = ViewportToWorldOnZPlane(cam, vx, vy, z);

        // push further out by margin in world X
        world.x += (side == SpawnSide.Left) ? -data.offscreenMarginX : data.offscreenMarginX;

        // Clamp Y relative to player and top-of-screen ON SAME PLANE
        float minY = player.position.y + data.minAbovePlayerY;
        float topY = ViewportToWorldOnZPlane(cam, 0.5f, 1f, z).y;
        float maxY = topY - Mathf.Max(0f, data.maxBelowTopWorld);

        world.y = Mathf.Clamp(world.y, minY, maxY);
        world.z = z;

        return world;
    }

    private static Vector3 ViewportToWorldOnZPlane(Camera cam, float vx, float vy, float targetZ)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, targetZ)); // world Z plane

        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        // fallback (shouldn't happen unless camera forward is parallel to plane)
        return cam.transform.position + cam.transform.forward * 20f;
    }
}

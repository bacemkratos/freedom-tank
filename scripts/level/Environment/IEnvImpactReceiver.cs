using UnityEngine;

public interface IEnvImpactReceiver
{
    /// <param name="effectId">Which effect to try to spawn (hole, scorch, spark...)</param>
    /// <param name="point">World contact point</param>
    /// <param name="normal">Surface normal (for rotation)</param>
    /// <param name="source">Optional: who hit (projectile) if you want</param>
    void OnEnvImpact(int effectId, Vector3 point, Vector3 normal, GameObject source);
}
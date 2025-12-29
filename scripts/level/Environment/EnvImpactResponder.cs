using System;
using System.Collections.Generic;
using UnityEngine;

public class EnvImpactResponder : MonoBehaviour, IEnvImpactReceiver
{
    [Serializable]
    public class EffectEntry
    {
        public int id;
        public GameObject prefab;

        [Tooltip("If true, rotate effect so its UP axis matches the hit normal.")]
        public bool alignToNormal = true;

        [Tooltip("If true, effect becomes child of the hit object.")]
        public bool parentToHitObject = true;
    }

    [Header("Effects by ID")]
    public List<EffectEntry> effects = new List<EffectEntry>();

    private readonly Dictionary<int, EffectEntry> map = new Dictionary<int, EffectEntry>();

    private void Awake()
    {
        map.Clear();

        foreach (var e in effects)
        {
            if (e == null) continue;

            if (!map.ContainsKey(e.id))
                map.Add(e.id, e);
        }
    }

    public void OnEnvImpact(int effectId, Vector3 point, Vector3 normal, GameObject hitObject)
    {
        if (!map.TryGetValue(effectId, out var entry)) return;
        if (entry.prefab == null) return;

        Quaternion rot = Quaternion.identity;

        if (entry.alignToNormal && normal.sqrMagnitude > 0.0001f)
            rot = Quaternion.FromToRotation(Vector3.up, normal.normalized);

        Transform parent = (entry.parentToHitObject && hitObject != null)
            ? hitObject.transform
            : null;

        Instantiate(entry.prefab, point, rot, parent);
    }
}

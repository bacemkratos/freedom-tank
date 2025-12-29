using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponSwitcher : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("WeaponId of the default infinite weapon (minigun).")]
    [SerializeField] private string defaultWeaponId = "minigun";

    [Tooltip("If true, will wrap around when scrolling beyond ends.")]
    [SerializeField] private bool wrap = true;

    [Tooltip("Optional: require cooldown between scroll switches.")]
    [SerializeField] private float switchCooldown = 0.08f;

    private readonly List<WeaponBase> _weapons = new();
    private int _activeIndex = -1;
    private float _nextSwitchTime;

    private void Awake()
    {
        RefreshWeaponList();
        ActivateDefaultOrFirstUsable();
    }

    private void OnEnable()
    {
        // If later you raise LevelStartEvent etc., you can reset/re-enable here if needed.
        EventBus.Subscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Subscribe<LevelEndEvent>(OnLevelEnd);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<LevelStartEvent>(OnLevelStart);
        EventBus.Unsubscribe<LevelEndEvent>(OnLevelEnd);
    }

    private void Update()
    {
        HandleScrollSwitch();

        // Example fire input (replace with your input system):
        if (Input.GetMouseButton(0))
            FireActive();
    }

    private void HandleScrollSwitch()
    {
        if (Time.time < _nextSwitchTime) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        int dir = scroll > 0 ? +1 : -1;
        SwitchToNextUsable(dir);

        _nextSwitchTime = Time.time + switchCooldown;
    }

    public void FireActive()
    {
        var w = GetActiveWeapon();
        if (w == null)
        {
            ActivateDefaultOrFirstUsable();
            return;
        }

        bool fired = w.TryFire();

        // If we couldn't fire because ammo became 0, fallback
        if (!fired && !w.IsUsable)
        {
            ActivateDefaultOrFirstUsable();
        }
        else if (!w.IsUsable) // after firing, ammo could become 0
        {
            ActivateDefaultOrFirstUsable();
        }
    }

    private WeaponBase GetActiveWeapon()
    {
        if (_activeIndex < 0 || _activeIndex >= _weapons.Count) return null;
        return _weapons[_activeIndex];
    }

    private void SwitchToNextUsable(int dir)
    {
        if (_weapons.Count == 0) return;

        int start = _activeIndex;
        int idx = _activeIndex;

        // if none active yet, start from default if possible
        if (idx < 0) idx = FindIndexById(defaultWeaponId);
        if (idx < 0) idx = 0;

        for (int steps = 0; steps < _weapons.Count; steps++)
        {
            idx += dir;

            if (wrap)
            {
                if (idx < 0) idx = _weapons.Count - 1;
                if (idx >= _weapons.Count) idx = 0;
            }
            else
            {
                idx = Mathf.Clamp(idx, 0, _weapons.Count - 1);
            }

            if (_weapons[idx].IsUsable)
            {
                SetActiveWeapon(idx);
                return;
            }
        }

        // No usable weapon except maybe default (infinite). Force default.
        ActivateDefaultOrFirstUsable();
    }

    private void ActivateDefaultOrFirstUsable()
    {
        int defaultIdx = FindIndexById(defaultWeaponId);
        if (defaultIdx >= 0)
        {
            SetActiveWeapon(defaultIdx);
            return;
        }

        // If default not found, choose first usable
        int firstUsable = _weapons.FindIndex(w => w.IsUsable);
        if (firstUsable >= 0)
        {
            SetActiveWeapon(firstUsable);
            return;
        }

        // If none usable, still pick first (will be inactive firing anyway)
        if (_weapons.Count > 0)
            SetActiveWeapon(0);
    }

    private void SetActiveWeapon(int index)
    {
        if (index < 0 || index >= _weapons.Count) return;

        for (int i = 0; i < _weapons.Count; i++)
            _weapons[i].SetActive(i == index);

        _activeIndex = index;

        EventBus.Raise(new WeaponChangedEvent(_weapons[_activeIndex].WeaponId));
    }

    private int FindIndexById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return -1;
        return _weapons.FindIndex(w => w.WeaponId == id);
    }

    private void RefreshWeaponList()
    {
        _weapons.Clear();

        // Only weapons under this main_weapon parent
        var found = GetComponentsInChildren<WeaponBase>(true).ToList();

        // Keep stable order (Hierarchy order)
        // Unity doesn't guarantee order, but usually is ok.
        // If you want explicit order, add an "order" int to WeaponBase.
        _weapons.AddRange(found);

        // Ensure all are disabled initially
        foreach (var w in _weapons)
            w.SetActive(false);
    }

    private void OnLevelStart(LevelStartEvent e)
    {
        // optional: reset weapon selection at level start
        ActivateDefaultOrFirstUsable();
    }

    private void OnLevelEnd(LevelEndEvent e)
    {
        // optional: disable firing, etc.
    }
}

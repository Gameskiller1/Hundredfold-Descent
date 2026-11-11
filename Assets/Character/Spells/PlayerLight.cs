
using System;
using UnityEngine;

/// <summary>
/// The player's "Light" (mana). Put it on the same object as PlayerController.
/// Spells spend it; pickups/torches can add it.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLight : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxLight = 100f;
    [SerializeField, Min(0f)] private float startLight = 100f;

    [Header("Regen")]
    [Tooltip("Light per second. 0 = no regen (only pickups refill).")]
    [SerializeField, Min(0f)] private float regenPerSecond = 5f;
    [Tooltip("Seconds after spending before regen starts.")]
    [SerializeField, Min(0f)] private float regenDelay = 1f;

    public float Current { get; private set; }
    public float Max => maxLight;
    public float Normalized => Current / maxLight;   // 0..1, for UI bars

    /// <summary>(current, max) - for UI.</summary>
    public event Action<float, float> Changed;

    private float lastSpendTime = -999f;

    private void Awake()
    {
        Current = Mathf.Clamp(startLight, 0f, maxLight);
    }

    private void Update()
    {
        // Uses scaled time: no regen while the game is frozen (aiming, pause)
        if (regenPerSecond <= 0f || Current >= maxLight) return;
        if (Time.time < lastSpendTime + regenDelay) return;
        Set(Current + regenPerSecond * Time.deltaTime);
    }

    public bool CanAfford(float amount) => Current >= amount;

    /// <summary>Spends light if there's enough. Returns false (and spends nothing) otherwise.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (Current < amount) return false;
        lastSpendTime = Time.time;
        Set(Current - amount);
        return true;
    }

    public void Add(float amount) => Set(Current + amount);

    public void SetMax(float newMax, bool fill = false)
    {
        maxLight = Mathf.Max(1f, newMax);
        Set(fill ? maxLight : Current);
    }

    private void Set(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxLight);
        if (Mathf.Approximately(clamped, Current)) return;
        Current = clamped;
        Changed?.Invoke(Current, maxLight);
    }
}

using System;
using UnityEngine;

/// <summary>
/// The player's "Light" (mana). Put it on the same object as PlayerController (+ Health).
/// Spells spend it; pickups/torches can add it.
/// OVERDRAW: with too little Light a spell can still be cast - the missing Light is paid in health.
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

    [Header("Overdraw (casting without enough Light hurts)")]
    [SerializeField] private bool overdrawCostsHealth = true;
    [Tooltip("HP lost per 1 missing Light. Spell costs 10, you have 4 -> 6 missing -> 6 HP at 1.0.")]
    [SerializeField, Min(0f)] private float healthPerMissingLight = 1f;
    [Tooltip("Off = a spell that would kill you is refused. On = you can die casting.")]
    [SerializeField] private bool overdrawCanKill = false;

    public float Current { get; private set; }
    public float Max => maxLight;
    public float Normalized => Current / maxLight;   // 0..1, for UI bars

    /// <summary>(current, max) - for UI.</summary>
    public event Action<float, float> Changed;
    /// <summary>(missing Light, health lost) - a spell was paid for with health.</summary>
    public event Action<float, float> Overdrew;

    private Health health;
    private float lastSpendTime = -999f;

    private void Awake()
    {
        Current = Mathf.Clamp(startLight, 0f, maxLight);
        health = GetComponent<Health>();
    }

    private void Update()
    {
        // Uses scaled time: no regen while the game is frozen (aiming, pause)
        if (regenPerSecond <= 0f || Current >= maxLight) return;
        if (Time.time < lastSpendTime + regenDelay) return;
        Set(Current + regenPerSecond * Time.deltaTime);
    }

    /// <summary>True if the Light is enough, or (overdraw) the player can pay the difference in health.</summary>
    public bool CanAfford(float amount)
    {
        if (Current >= amount) return true;
        if (!overdrawCostsHealth || health == null || !health.IsAlive) return false;
        return overdrawCanKill || health.Current > (amount - Current) * healthPerMissingLight;
    }

    /// <summary>Spends Light (and health for the missing part). Returns false and spends nothing if it can't.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (!CanAfford(amount)) return false;

        lastSpendTime = Time.time;
        if (Current >= amount) { Set(Current - amount); return true; }

        float missing = amount - Current;
        float damage = missing * healthPerMissingLight;
        Set(0f);
        health.TakeDamage(new DamageInfo(damage, Faction.Neutral, gameObject, DamageType.Pure) { ignoreInvulnerability = true });
        Overdrew?.Invoke(missing, damage);
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
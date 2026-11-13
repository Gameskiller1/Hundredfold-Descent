using System;
using UnityEngine;

/// <summary>
/// Hit points for ANYTHING (player, enemies, bosses, breakables). Collider on the same object
/// (or a child) is what attacks hit - put it on the right layer.
/// Never destroys anything itself: listen to Died and react (Enemy / PlayerHealth do).
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Serializable]
    public struct Resistance
    {
        public DamageType type;
        [Tooltip("0 = immune, 0.5 = takes half, 2 = takes double.")]
        public float multiplier;
    }

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Team")]
    [Tooltip("Enemy / PlayerHealth set this automatically when left on Neutral.")]
    [SerializeField] private Faction faction = Faction.Neutral;
    [Tooltip("Off = hits from the same faction are ignored.")]
    [SerializeField] private bool allowFriendlyFire = false;

    [Header("Invulnerability")]
    [Tooltip("Seconds of immunity after taking a hit. Player ~0.8, most enemies 0.")]
    [SerializeField, Min(0f)] private float invulnerableAfterHit = 0f;

    [Header("Resistances (optional)")]
    [SerializeField] private Resistance[] resistances;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public float Normalized => Current / maxHealth;   // 0..1, for UI bars / boss phases
    public bool IsAlive => Current > 0f;
    public Faction Faction => faction;
    public Health Owner => this;
    public float DamageMultiplier => 1f;

    /// <summary>The short immunity after a hit (flicker effects use this).</summary>
    public bool RecentlyHit => Time.time < invulnerableUntil;
    /// <summary>Short post-hit immunity OR any invulnerability lock.</summary>
    public bool IsInvulnerable => invulnerableLocks > 0 || RecentlyHit;

    /// <summary>(current, max) - any change. For UI.</summary>
    public event Action<float, float> Changed;
    /// <summary>A hit landed (amount is what was actually dealt). Also fires for the killing blow.</summary>
    public event Action<DamageInfo> Damaged;
    public event Action<float> Healed;
    public event Action<DamageInfo> Died;
    public event Action Revived;

    /// <summary>
    /// One owner can set this to scale incoming damage (armor phases, dash i-frames...).
    /// Return 1 for normal, 0 to block.
    /// </summary>
    public Func<DamageInfo, float> DamageModifier;

    private int invulnerableLocks;
    private float invulnerableUntil;

    private void Awake()
    {
        Current = maxHealth;
    }

    // =====================  DAMAGE  =====================

    public bool TakeDamage(DamageInfo info)
    {
        if (!IsAlive || info.amount <= 0f) return false;
        if (!allowFriendlyFire && info.faction != Faction.Neutral && info.faction == faction) return false;
        if (IsInvulnerable && !info.ignoreInvulnerability) return false;

        float amount = info.amount;
        if (info.type != DamageType.Pure) amount *= ResistanceFor(info.type);
        if (DamageModifier != null) amount *= DamageModifier(info);
        if (amount <= 0f) return false;

        info.amount = amount;
        invulnerableUntil = Time.time + invulnerableAfterHit;
        Current = Mathf.Max(0f, Current - amount);

        Changed?.Invoke(Current, maxHealth);
        Damaged?.Invoke(info);
        if (Current <= 0f) Died?.Invoke(info);
        return true;
    }

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        float before = Current;
        Current = Mathf.Min(maxHealth, Current + amount);
        if (Current <= before) return;
        Healed?.Invoke(Current - before);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>Instant death (pits, scripted kills). Skips the Damaged event.</summary>
    public void Kill()
    {
        if (!IsAlive) return;
        var info = new DamageInfo(Current, Faction.Neutral, null, DamageType.Pure) { ignoreInvulnerability = true };
        Current = 0f;
        Changed?.Invoke(Current, maxHealth);
        Died?.Invoke(info);
    }

    public void Revive(float fraction = 1f)
    {
        Current = Mathf.Clamp(maxHealth * fraction, 1f, maxHealth);
        invulnerableUntil = 0f;
        Changed?.Invoke(Current, maxHealth);
        Revived?.Invoke();
    }

    public void SetMax(float newMax, bool fill = false)
    {
        maxHealth = Mathf.Max(1f, newMax);
        Current = fill ? maxHealth : Mathf.Min(Current, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    public void SetFaction(Faction newFaction) => faction = newFaction;

    // ---------- Invulnerability locks (boss phase changes, cutscenes...) ----------
    public void LockInvulnerable() => invulnerableLocks++;
    public void UnlockInvulnerable() => invulnerableLocks = Mathf.Max(0, invulnerableLocks - 1);

    // =====================  HELPERS  =====================

    private float ResistanceFor(DamageType type)
    {
        if (resistances == null) return 1f;
        foreach (var r in resistances)
            if (r.type == type) return Mathf.Max(0f, r.multiplier);
        return 1f;
    }
}

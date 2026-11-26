using System;
using UnityEngine;

/// <summary>
/// Put on a child collider that forwards hits to the parent's Health, with a damage multiplier.
/// Weak points (head x2), armored parts (x0.25), separate boss limbs...
/// </summary>
public class Hurtbox : MonoBehaviour, IDamageable
{
    [Tooltip("Empty = the Health on a parent object.")]
    [SerializeField] private Health health;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    /// <summary>This specific part was hit (flash it, play a sound, break armor...).</summary>
    public event Action<DamageInfo> Hit;

    public bool IsAlive => health != null && health.IsAlive;
    public Faction Faction => health != null ? health.Faction : Faction.Neutral;
    public Health Owner => health;
    public float DamageMultiplier => damageMultiplier;

    private void Awake()
    {
        if (health == null) health = GetComponentInParent<Health>();
    }

    public bool TakeDamage(DamageInfo info)
    {
        if (health == null) return false;
        info.amount *= damageMultiplier;
        bool landed = health.TakeDamage(info);
        if (landed) Hit?.Invoke(info);
        return landed;
    }
}

using UnityEngine;

/// <summary>Teams. A hit never lands on its own faction (unless that Health allows friendly fire).</summary>
public enum Faction { Neutral, Player, Enemy }

/// <summary>
/// Add new types at the END (keeps saved inspector values valid).
/// Health.resistances works per type; Pure ignores resistances.
/// </summary>
public enum DamageType { Physical, Lightning, Explosion, Fire, Poison, Magic, Pure }

/// <summary>Everything about one hit. Build it, hand it to IDamageable.TakeDamage.</summary>
public struct DamageInfo
{
    public float amount;
    public DamageType type;
    public Faction faction;               // who is attacking
    public GameObject source;             // what dealt it (may be null)
    public Vector2 point;                 // world position of the hit (for effects)
    public Vector2 knockback;             // velocity the target gets pushed with (direction included)
    public bool ignoreInvulnerability;    // traps, execution, scripted damage...

    public DamageInfo(float amount, Faction faction, GameObject source = null, DamageType type = DamageType.Physical)
    {
        this.amount = amount;
        this.faction = faction;
        this.source = source;
        this.type = type;
        point = source != null ? (Vector2)source.transform.position : Vector2.zero;
        knockback = Vector2.zero;
        ignoreInvulnerability = false;
    }

    /// <summary>Platformer knockback: sideways away from 'from', plus a fixed upward kick.</summary>
    public static Vector2 KnockbackAway(Vector2 from, Vector2 to, float horizontal, float vertical)
    {
        float side = to.x >= from.x ? 1f : -1f;
        return new Vector2(side * horizontal, vertical);
    }
}

/// <summary>Anything that can be hurt: Health, Hurtbox (weak points / boss parts)...</summary>
public interface IDamageable
{
    bool IsAlive { get; }
    Faction Faction { get; }
    /// <summary>The Health this belongs to (several Hurtboxes can share one).</summary>
    Health Owner { get; }
    /// <summary>1 for Health; Hurtboxes report their multiplier (used to pick the best part when several overlap).</summary>
    float DamageMultiplier { get; }

    /// <returns>true if the hit landed (false = dead, immune, invulnerable, friendly fire...).</returns>
    bool TakeDamage(DamageInfo info);
}

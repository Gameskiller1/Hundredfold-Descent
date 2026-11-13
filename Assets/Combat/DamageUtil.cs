using System.Collections.Generic;
using UnityEngine;

public struct DamageTarget
{
    public IDamageable Damageable;   // null only for non-damageable physics props (damageablesOnly = false)
    public Collider2D Collider;
}

/// <summary>Shared hit detection so every attack (player, enemy, spell) behaves the same.</summary>
public static class DamageUtil
{
    /// <summary>Finds the IDamageable on a collider or its parents (Hurtbox first, then Health).</summary>
    public static IDamageable Find(Component c) => c != null ? c.GetComponentInParent<IDamageable>() : null;

    /// <summary>
    /// Everything alive inside the area, ONE entry per owner (several colliders / hurtboxes on the
    /// same enemy count once - the part with the highest damage multiplier wins).
    /// Returns a new list each call, so it's safe to damage while looping.
    /// </summary>
    public static List<DamageTarget> Box(Vector2 center, Vector2 size, float angle, LayerMask mask, bool damageablesOnly = true)
        => Collect(Physics2D.OverlapBoxAll(center, size, angle, mask), damageablesOnly);

    public static List<DamageTarget> Circle(Vector2 center, float radius, LayerMask mask, bool damageablesOnly = true)
        => Collect(Physics2D.OverlapCircleAll(center, radius, mask), damageablesOnly);

    private static List<DamageTarget> Collect(Collider2D[] colliders, bool damageablesOnly)
    {
        var result = new List<DamageTarget>(colliders.Length);
        var index = new Dictionary<UnityEngine.Object, int>();

        foreach (var c in colliders)
        {
            IDamageable d = Find(c);
            if (d != null && !d.IsAlive) continue;
            if (d == null && damageablesOnly) continue;

            // One entry per owner (damageables) or per body (props)
            UnityEngine.Object key;
            if (d != null && d.Owner != null) key = d.Owner;
            else if (c.attachedRigidbody != null) key = c.attachedRigidbody;
            else key = c.transform.root;

            var entry = new DamageTarget { Damageable = d, Collider = c };

            if (index.TryGetValue(key, out int i))
            {
                if (d != null && d.DamageMultiplier > result[i].Damageable.DamageMultiplier) result[i] = entry;
                continue;
            }

            index[key] = result.Count;
            result.Add(entry);
        }
        return result;
    }
}

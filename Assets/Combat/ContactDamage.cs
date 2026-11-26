using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Touching this hurts. Put on the enemy (same object as its collider and Rigidbody2D).
/// Works with a normal collider or a trigger. Stops automatically when the owner dies.
/// </summary>
public class ContactDamage : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Layer(s) of whatever this can hurt (the Player's layer).")]
    [SerializeField] private LayerMask hitLayers;
    [SerializeField, Min(0f)] private float knockbackX = 6f;
    [SerializeField, Min(0f)] private float knockbackY = 4f;
    [Tooltip("Minimum seconds between hits on the same target (on top of the target's own i-frames).")]
    [SerializeField, Min(0f)] private float rehitInterval = 0.5f;

    private Health ownerHealth;
    private Enemy owner;
    private readonly Dictionary<int, float> lastHit = new Dictionary<int, float>();

    private void Awake()
    {
        ownerHealth = GetComponentInParent<Health>();
        owner = GetComponentInParent<Enemy>();
    }

    private void OnCollisionEnter2D(Collision2D c) => TryHit(c.collider);
    private void OnCollisionStay2D(Collision2D c) => TryHit(c.collider);
    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (!enabled) return;
        if (ownerHealth != null && !ownerHealth.IsAlive) return;
        if (owner != null && owner.IsDormant) return;
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        int id = other.GetInstanceID();
        if (lastHit.TryGetValue(id, out float last) && Time.time < last + rehitInterval) return;

        IDamageable target = DamageUtil.Find(other);
        if (target == null || !target.IsAlive) return;

        Faction team = ownerHealth != null ? ownerHealth.Faction : Faction.Enemy;
        float scale = owner != null ? owner.DamageMultiplier : 1f;

        var info = new DamageInfo(damage * scale, team, gameObject, damageType);
        info.point = other.bounds.center;
        info.knockback = DamageInfo.KnockbackAway(transform.position, other.bounds.center, knockbackX, knockbackY);

        if (target.TakeDamage(info)) lastHit[id] = Time.time;
    }
}

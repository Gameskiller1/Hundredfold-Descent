using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bullet / fireball / arrow. Prefab needs: Rigidbody2D (Kinematic for straight shots, Dynamic
/// with gravity for arcs) + a trigger Collider2D. Draw the sprite facing RIGHT.
/// Spawned and launched by ProjectileEnemyAttack (or any script via Launch).
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField, Min(0f)] private float speed = 8f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [Tooltip("Turns to face where it's flying (arcs).")]
    [SerializeField] private bool rotateToVelocity = true;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [SerializeField, Min(0f)] private float knockbackX = 4f;
    [SerializeField, Min(0f)] private float knockbackY = 2f;

    [Header("Collision")]
    [Tooltip("Layer(s) it can hurt (the Player's layer for enemy shots).")]
    [SerializeField] private LayerMask targetLayers;
    [Tooltip("Layer(s) that stop it (Ground).")]
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("On = flies through targets instead of vanishing on the first hit.")]
    [SerializeField] private bool pierce = false;

    [Header("Optional")]
    [Tooltip("Spawned where it hits something.")]
    [SerializeField] private GameObject impactPrefab;

    private Rigidbody2D rb;
    private Faction faction;
    private GameObject source;
    private float damageScale = 1f;
    private bool spent;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    private void Awake() => rb = GetComponent<Rigidbody2D>();

    /// <summary>faction = the shooter's team: same-team targets are ignored.</summary>
    public void Launch(Vector2 direction, Faction faction, GameObject source, float damageScale = 1f)
    {
        this.faction = faction;
        this.source = source;
        this.damageScale = damageScale;

        rb.linearVelocity = direction.normalized * speed;
        Orient();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (rotateToVelocity) Orient();
    }

    private void Orient()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f) transform.right = rb.linearVelocity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spent) return;
        int bit = 1 << other.gameObject.layer;

        if ((obstacleLayers.value & bit) != 0) { Despawn(); return; }
        if ((targetLayers.value & bit) == 0) return;

        IDamageable target = DamageUtil.Find(other);
        if (target == null || !target.IsAlive) return;
        if (faction != Faction.Neutral && target.Faction == faction) return;   // friendly: fly through
        if (!alreadyHit.Add(target)) return;

        var info = new DamageInfo(damage * damageScale, faction, source, damageType);
        info.point = transform.position;
        info.knockback = DamageInfo.KnockbackAway(transform.position, other.bounds.center, knockbackX, knockbackY);
        target.TakeDamage(info);

        if (!pierce) Despawn();
    }

    private void Despawn()
    {
        spent = true;
        if (impactPrefab != null) Instantiate(impactPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}

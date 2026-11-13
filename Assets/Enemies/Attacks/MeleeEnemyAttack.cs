using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swipe / bite / slam. Windup -> hit window -> recovery.
/// "Both Sides" turns it into a ground-slam style attack around the enemy.
/// </summary>
public class MeleeEnemyAttack : EnemyAttack
{
    [Header("Timing (seconds)")]
    [Tooltip("Telegraph before the hit. Match the animation's wind-up.")]
    [SerializeField, Min(0f)] private float windup = 0.4f;
    [Tooltip("How long the hit box stays active (each target is hit once).")]
    [SerializeField, Min(0f)] private float activeTime = 0.1f;
    [SerializeField, Min(0f)] private float recovery = 0.4f;

    [Header("Hit Box")]
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.2f, 1f);
    [Tooltip("Relative to the enemy's pivot. X is mirrored by facing.")]
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.8f, 0.5f);
    [Tooltip("Also hits behind the enemy (slams, spins).")]
    [SerializeField] private bool bothSides = false;
    [SerializeField] private AttackDamage damage = new AttackDamage();

    [Header("Optional")]
    [Tooltip("Spawned when the hit starts (shockwave, slash VFX). Should destroy itself or use Effect Lifetime.")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField, Min(0f)] private float effectLifetime = 2f;

    private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();

    protected override IEnumerator Execute()
    {
        hitThisSwing.Clear();
        PlayAnimation(windup + activeTime + recovery);

        yield return Wait(windup);

        if (effectPrefab != null)
        {
            var fx = Instantiate(effectPrefab, Local(hitBoxOffset), Quaternion.identity);
            if (effectLifetime > 0f) Destroy(fx, effectLifetime);
        }

        float t = 0f;
        do
        {
            CheckHit();
            t += Time.deltaTime;
            yield return null;
        } while (t < activeTime);

        yield return Wait(recovery);
    }

    private void CheckHit()
    {
        HitBox(Local(hitBoxOffset), hitBoxSize, damage, hitThisSwing);
        if (bothSides) HitBox(Local(hitBoxOffset, -enemy.FacingSign), hitBoxSize, damage, hitThisSwing);
    }

    private void OnDrawGizmosSelected()
    {
        var e = GetComponentInParent<Enemy>();
        int facing = e != null ? e.FacingSign : 1;
        Vector3 root = e != null ? e.transform.position : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(root + new Vector3(hitBoxOffset.x * facing, hitBoxOffset.y), hitBoxSize);
        if (bothSides) Gizmos.DrawWireCube(root + new Vector3(-hitBoxOffset.x * facing, hitBoxOffset.y), hitBoxSize);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>Shoots one or more projectiles (spit, fireball, arrow...). Windup -> shoot -> recovery.</summary>
public class ProjectileEnemyAttack : EnemyAttack
{
    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Where it spawns, relative to the enemy's pivot. X is mirrored by facing.")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.6f, 0.6f);
    [Tooltip("Off = shoots straight ahead instead of at the player.")]
    [SerializeField] private bool aimAtTarget = true;

    [Header("Pattern")]
    [SerializeField, Min(1)] private int count = 1;
    [Tooltip("Total fan angle across all shots (degrees).")]
    [SerializeField, Min(0f)] private float spreadAngle = 20f;
    [Tooltip("Seconds between shots. 0 = all at once.")]
    [SerializeField, Min(0f)] private float shotInterval = 0f;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0f)] private float windup = 0.5f;
    [SerializeField, Min(0f)] private float recovery = 0.5f;

    protected override IEnumerator Execute()
    {
        PlayAnimation(windup + recovery + shotInterval * Mathf.Max(0, count - 1));
        yield return Wait(windup);

        for (int i = 0; i < count; i++)
        {
            Fire(i);
            if (shotInterval > 0f && i < count - 1) yield return Wait(shotInterval);
        }

        yield return Wait(recovery);
    }

    private void Fire(int index)
    {
        if (projectilePrefab == null) { Debug.LogWarning("[ProjectileAttack] No projectile prefab assigned.", this); return; }

        Vector2 muzzle = Local(muzzleOffset);
        Vector2 dir = new Vector2(enemy.FacingSign, 0f);
        if (aimAtTarget && enemy.HasTarget) dir = (enemy.TargetCenter - muzzle).normalized;

        float angle = count > 1 ? Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, index / (float)(count - 1)) : 0f;
        dir = Quaternion.Euler(0f, 0f, angle) * dir;

        var shot = Instantiate(projectilePrefab, muzzle, Quaternion.identity);
        shot.Launch(dir, enemy.Faction, enemy.gameObject, enemy.DamageMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        var e = GetComponentInParent<Enemy>();
        int facing = e != null ? e.FacingSign : 1;
        Vector3 root = e != null ? e.transform.position : transform.position;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(root + new Vector3(muzzleOffset.x * facing, muzzleOffset.y), 0.1f);
    }
}

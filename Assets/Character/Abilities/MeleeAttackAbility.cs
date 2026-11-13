using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's main attack. Put it on AbilitiesChild next to Dash/Jump and assign the "Attack" input.
/// Plays the Attack animation, then hits everything in front of the player (once each).
/// Enemies need Health + a collider on a layer included in Hit Layers.
/// </summary>
public class MeleeAttackAbility : PlayerAbility
{
    [Header("Attack")]
    [SerializeField, Min(0f)] private float damage = 15f;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Seconds from pressing the button until the hit lands (match the swing frame).")]
    [SerializeField, Min(0f)] private float hitDelay = 0.1f;
    [Tooltip("Total attack time. <= 0 = length of the Attack animation.")]
    [SerializeField] private float attackDuration = -1f;
    [SerializeField] private bool allowInAir = true;
    [Tooltip("Stand still while swinging. Off = keep walking.")]
    [SerializeField] private bool lockMovement = true;

    [Header("Hit Box")]
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.4f, 1.2f);
    [Tooltip("Relative to the player's pivot. X is mirrored by facing.")]
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.9f, 0.6f);
    [Tooltip("Set to the Enemies layer (and breakables...).")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Knockback (on enemies)")]
    [SerializeField, Min(0f)] private float knockbackX = 5f;
    [SerializeField, Min(0f)] private float knockbackY = 2f;

    public bool IsAttacking { get; private set; }

    /// <summary>Fires for every enemy/object hit (hit-stop, sounds, gain Light...).</summary>
    public event System.Action<Health> Hit;

    private bool movementLocked;

    protected override bool CanActivate() => !IsAttacking && (allowInAir || player.IsGrounded);

    protected override void Activate() => StartCoroutine(AttackRoutine());

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;

        float total = attackDuration > 0f ? attackDuration
                    : (player.Anim != null ? player.Anim.GetLength(PlayerAnim.Attack) : 0.3f);
        float delay = Mathf.Min(hitDelay, total);

        if (lockMovement)
        {
            player.LockMovement();
            movementLocked = true;
            player.StopHorizontal();
        }

        player.Anim?.PlayOneShot(PlayerAnim.Attack, null, total);

        yield return new WaitForSeconds(delay);
        DoHit();
        yield return new WaitForSeconds(total - delay);

        EndAttack();
    }

    private void DoHit()
    {
        Vector2 origin = player.transform.position;
        Vector2 center = origin + new Vector2(hitBoxOffset.x * player.FacingSign, hitBoxOffset.y);

        foreach (var t in DamageUtil.Box(center, hitBoxSize, 0f, hitLayers))
        {
            Vector2 at = t.Collider.bounds.center;
            var info = new DamageInfo(damage, Faction.Player, gameObject, damageType);
            info.point = at;
            info.knockback = DamageInfo.KnockbackAway(origin, at, knockbackX, knockbackY);

            if (t.Damageable.TakeDamage(info)) Hit?.Invoke(t.Damageable.Owner);
        }
    }

    private void EndAttack()
    {
        if (movementLocked) { movementLocked = false; player.UnlockMovement(); }
        IsAttacking = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (IsAttacking) { StopAllCoroutines(); EndAttack(); }   // never leave the player locked
    }

    private void OnDrawGizmosSelected()
    {
        var p = GetComponentInParent<PlayerController>();
        int facing = p != null ? p.FacingSign : 1;
        Vector3 root = p != null ? p.transform.position : transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(root + new Vector3(hitBoxOffset.x * facing, hitBoxOffset.y), hitBoxSize);
    }
}

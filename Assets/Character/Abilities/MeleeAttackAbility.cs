using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's main attack (LMB). Put it on AbilitiesChild next to Dash/Jump, assign the "Attack" input.
/// Two hit zones, each can be turned on/off:
///   - Front box: the swing in front of the player (mirrored by facing)
///   - Around circle: close-range hit all around the player (stops enemies hugging your back)
/// Timing: windup -> active window (hit zones live, each target hit once) -> recovery.
/// </summary>
public class MeleeAttackAbility : PlayerAbility
{
    [Header("Damage")]
    [SerializeField, Min(0f)] private float damage = 15f;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Set to the Enemies layer (and breakables).")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Timing (seconds)")]
    [Tooltip("From click until the hit zones turn on (match the swing frame).")]
    [SerializeField, Min(0f)] private float windup = 0.1f;
    [Tooltip("How long the hit zones stay on.")]
    [SerializeField, Min(0f)] private float activeTime = 0.1f;
    [Tooltip("After the hit, before you can act again. <0 = rest of the Attack animation.")]
    [SerializeField] private float recovery = -1f;

    [Header("Rules")]
    [SerializeField] private bool allowInAir = true;
    [Tooltip("Stand still while swinging. Off = keep walking.")]
    [SerializeField] private bool lockMovement = true;

    [Header("Front Hit Box")]
    [SerializeField] private bool useFrontBox = true;
    [SerializeField] private Vector2 frontSize = new Vector2(1.4f, 1.2f);
    [Tooltip("From the player's pivot. X is mirrored by facing.")]
    [SerializeField] private Vector2 frontOffset = new Vector2(0.9f, 0.6f);
    [SerializeField, Min(0f)] private float frontDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float frontKnockbackX = 5f;
    [SerializeField, Min(0f)] private float frontKnockbackY = 2f;

    [Header("Around Hit Circle")]
    [SerializeField] private bool useAroundCircle = true;
    [SerializeField, Min(0f)] private float aroundRadius = 0.8f;
    [SerializeField] private Vector2 aroundOffset = new Vector2(0f, 0.6f);
    [Tooltip("Usually weaker than the front swing.")]
    [SerializeField, Min(0f)] private float aroundDamageMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float aroundKnockbackX = 3f;
    [SerializeField, Min(0f)] private float aroundKnockbackY = 1f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = false;

    public bool IsAttacking { get; private set; }

    // Hooks (debug console, sounds, hit-stop, camera shake...)
    public event Action Started;              // after the click passed all checks
    public event Action HitWindowOpened;
    public event Action<Health, bool> Hit;    // (target, fromFrontBox)
    public event Action Finished;

    private readonly HashSet<Health> hitThisSwing = new HashSet<Health>();
    private bool movementLocked;

    protected override bool CanActivate() => !IsAttacking && (allowInAir || player.IsGrounded);

    protected override void Activate() => StartCoroutine(AttackRoutine());

    private IEnumerator AttackRoutine()
    {
        IsAttacking = true;
        hitThisSwing.Clear();
        Log("attack started");
        Started?.Invoke();

        float animLength = player.Anim != null ? player.Anim.GetLength(PlayerAnim.Attack) : 0.3f;
        float rec = recovery >= 0f ? recovery : Mathf.Max(0f, animLength - windup - activeTime);
        float total = windup + activeTime + rec;

        if (lockMovement)
        {
            player.LockMovement();
            movementLocked = true;
            player.StopHorizontal();
        }

        player.Anim?.PlayOneShot(PlayerAnim.Attack, null, total);

        if (windup > 0f) yield return new WaitForSeconds(windup);

        Log("hit window open");
        HitWindowOpened?.Invoke();
        float t = 0f;
        do
        {
            CheckHits();
            t += Time.deltaTime;
            yield return null;
        } while (t < activeTime);

        if (rec > 0f) yield return new WaitForSeconds(rec);

        EndAttack();
    }

    private void CheckHits()
    {
        Vector2 origin = player.transform.position;
        int f = player.FacingSign;

        // Front first: if a target is in both zones, it takes the front (stronger) hit
        if (useFrontBox)
        {
            Vector2 c = origin + new Vector2(frontOffset.x * f, frontOffset.y);
            foreach (var target in DamageUtil.Box(c, frontSize, 0f, hitLayers))
                Strike(target, origin, frontDamageMultiplier, frontKnockbackX, frontKnockbackY, true);
        }

        if (useAroundCircle)
        {
            Vector2 c = origin + aroundOffset;
            foreach (var target in DamageUtil.Circle(c, aroundRadius, hitLayers))
                Strike(target, origin, aroundDamageMultiplier, aroundKnockbackX, aroundKnockbackY, false);
        }
    }

    private void Strike(DamageTarget target, Vector2 origin, float mult, float kx, float ky, bool front)
    {
        Health owner = target.Damageable.Owner;
        if (owner != null && !hitThisSwing.Add(owner)) return;   // once per swing

        Vector2 at = target.Collider.bounds.center;
        var info = new DamageInfo(damage * mult, Faction.Player, gameObject, damageType);
        info.point = at;
        info.knockback = DamageInfo.KnockbackAway(origin, at, kx, ky);

        if (target.Damageable.TakeDamage(info))
        {
            Log($"hit {target.Collider.name} for {info.amount:0.#} ({(front ? "front" : "around")})");
            Hit?.Invoke(owner, front);
        }
    }

    private void EndAttack()
    {
        if (movementLocked) { movementLocked = false; player.UnlockMovement(); }
        if (!IsAttacking) return;
        IsAttacking = false;
        Log("attack finished");
        Finished?.Invoke();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (IsAttacking) { StopAllCoroutines(); EndAttack(); }   // never leave the player locked
    }

    private void Log(string msg)
    {
        if (logToConsole) Debug.Log($"[PlayerAttack] {msg}", this);
    }

    private void OnDrawGizmosSelected()
    {
        var p = GetComponentInParent<PlayerController>();
        int f = p != null ? p.FacingSign : 1;
        Vector3 root = p != null ? p.transform.position : transform.position;

        if (useFrontBox)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(root + new Vector3(frontOffset.x * f, frontOffset.y), frontSize);
        }
        if (useAroundCircle)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(root + (Vector3)aroundOffset, aroundRadius);
        }
    }
}
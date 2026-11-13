using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Damage settings shared by every attack type (shows as a foldout in the Inspector).</summary>
[Serializable]
public class AttackDamage
{
    [Min(0f)] public float damage = 10f;
    public DamageType type = DamageType.Physical;
    [Min(0f)] public float knockbackX = 5f;
    [Min(0f)] public float knockbackY = 3f;
    [Tooltip("Layer(s) of whatever it can hurt (the Player's layer).")]
    public LayerMask hitLayers;
}

/// <summary>
/// ONE move an enemy can do. Put each attack on its own child object (or the root) of the enemy.
/// The brain picks among the enabled ones that are ready and in range (weighted).
/// Boss phases turn attacks on/off with the component's checkbox.
///
/// To make a NEW attack: inherit this and implement Execute() as a coroutine:
///   protected override IEnumerator Execute() { PlayAnimation(); yield return Wait(0.5f); ...hit...; yield return Wait(0.4f); }
/// Locking, facing, cooldown, cancel-on-stun/death are handled for you.
/// </summary>
public abstract class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("Within this distance (centre to centre) the attack may start.")]
    [SerializeField, Min(0f)] private float maxRange = 1.5f;
    [SerializeField, Min(0f)] private float minRange = 0f;
    [Tooltip("Won't start if the player is higher/lower than this (other floor).")]
    [SerializeField, Min(0f)] private float maxVerticalOffset = 1.5f;
    [SerializeField, Min(0f)] private float cooldown = 1.5f;
    [Tooltip("Higher = picked more often when several attacks are possible.")]
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField] private bool needsLineOfSight = false;
    [SerializeField] private LayerMask sightBlockers;

    [Header("Animation")]
    [Tooltip("Animator state (and clip) name. Empty / missing = no animation, timing still works.")]
    [SerializeField] protected string animationState = "Attack";

    [Header("Behaviour")]
    [SerializeField] private bool faceTargetOnStart = true;
    [Tooltip("Stand still when it starts (turn off for attacks that keep momentum).")]
    [SerializeField] private bool stopOnStart = true;

    protected Enemy enemy;

    public bool IsRunning { get; private set; }
    public float Weight => weight;
    public float Cooldown => cooldown;

    /// <summary>Fires when it ends (finished OR cancelled).</summary>
    public event Action<EnemyAttack> Finished;

    private float readyTime;
    private Coroutine routine;
    private bool lockHeld;

    protected virtual void Awake() => enemy = GetComponentInParent<Enemy>();

    // =====================  CHECKS  =====================

    /// <summary>Extra conditions (minion limit, health below 50%...). Override if needed.</summary>
    protected virtual bool IsUsable() => true;

    public bool IsOffCooldown => Time.time >= readyTime;

    public bool CanStart(Vector2 targetCenter)
        => enabled && enemy != null && !IsRunning && IsOffCooldown && IsUsable() && InRange(targetCenter);

    public virtual bool InRange(Vector2 targetCenter)
    {
        Vector2 from = enemy.Center;
        float dist = Vector2.Distance(from, targetCenter);
        if (dist > maxRange || dist < minRange) return false;
        if (Mathf.Abs(targetCenter.y - from.y) > maxVerticalOffset) return false;
        if (needsLineOfSight && Physics2D.Linecast(from, targetCenter, sightBlockers).collider != null) return false;
        return true;
    }

    // =====================  RUNNING  =====================

    /// <summary>Starts the attack (the brain calls this). Returns false if it can't.</summary>
    public bool Begin()
    {
        if (enemy == null || IsRunning || !enabled) return false;
        routine = StartCoroutine(Run());
        return true;
    }

    /// <summary>Stops it right now (stun, death, phase change). Cooldown still starts.</summary>
    public void Cancel()
    {
        if (!IsRunning) return;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        OnCancelled();
        End();
    }

    private IEnumerator Run()
    {
        IsRunning = true;
        enemy.LockMovement();
        lockHeld = true;

        if (stopOnStart) enemy.StopHorizontal();
        if (faceTargetOnStart && enemy.HasTarget) enemy.FaceTowards(enemy.TargetCenter.x);

        yield return Execute();

        routine = null;
        End();
    }

    private void End()
    {
        if (lockHeld) { lockHeld = false; enemy.UnlockMovement(); }
        IsRunning = false;
        readyTime = Time.time + cooldown;
        Finished?.Invoke(this);
    }

    protected virtual void OnDisable() => Cancel();

    /// <summary>The attack itself: windup, hit, recovery.</summary>
    protected abstract IEnumerator Execute();

    /// <summary>Clean up (stop a lunge, etc.) when interrupted.</summary>
    protected virtual void OnCancelled() { }

    // =====================  HELPERS FOR SUBCLASSES  =====================

    protected void PlayAnimation(float duration = -1f) => enemy.Anim?.Play(animationState, duration);

    protected static WaitForSeconds Wait(float seconds) => new WaitForSeconds(Mathf.Max(0f, seconds));

    /// <summary>Point in front of the enemy: x is mirrored by facing.</summary>
    protected Vector2 Local(Vector2 offset, int? facing = null)
        => (Vector2)enemy.transform.position + new Vector2(offset.x * (facing ?? enemy.FacingSign), offset.y);

    /// <summary>Hits everything new inside the box. 'alreadyHit' makes each target get hit once per swing.</summary>
    protected int HitBox(Vector2 center, Vector2 size, AttackDamage d, HashSet<Health> alreadyHit)
    {
        int landed = 0;
        foreach (var t in DamageUtil.Box(center, size, 0f, d.hitLayers))
            if (Strike(t, d, alreadyHit)) landed++;
        return landed;
    }

    protected int HitCircle(Vector2 center, float radius, AttackDamage d, HashSet<Health> alreadyHit)
    {
        int landed = 0;
        foreach (var t in DamageUtil.Circle(center, radius, d.hitLayers))
            if (Strike(t, d, alreadyHit)) landed++;
        return landed;
    }

    private bool Strike(DamageTarget t, AttackDamage d, HashSet<Health> alreadyHit)
    {
        if (t.Damageable == null) return false;
        if (alreadyHit != null && t.Damageable.Owner != null && !alreadyHit.Add(t.Damageable.Owner)) return false;

        Vector2 at = t.Collider.bounds.center;
        var info = new DamageInfo(d.damage * enemy.DamageMultiplier, enemy.Faction, enemy.gameObject, d.type);
        info.point = at;
        info.knockback = DamageInfo.KnockbackAway(enemy.transform.position, at, d.knockbackX, d.knockbackY);
        return t.Damageable.TakeDamage(info);
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The enemy "core": goes on the ROOT of every enemy and boss (with Health + Rigidbody2D + a Collider2D).
/// It owns facing, the target (player), stun/knockback, death and locks. It does NOT decide what to do:
/// that's the Brain (EnemyBrain), movement is the Motor (EnemyMotor), moves are Attacks (EnemyAttack).
/// Bosses are just Enemies with BossPhases / BossEncounter added.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("Info")]
    [Tooltip("Shown on boss bars.")]
    [SerializeField] private string displayName = "Enemy";

    [Header("Stats (phases / difficulty change these at runtime)")]
    [SerializeField, Min(0f)] private float speedMultiplier = 1f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    [Header("Hit Reaction")]
    [Tooltip("0 = full knockback, 1 = immune (bosses).")]
    [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0f;
    [Tooltip("Seconds the enemy can't steer after being knocked back.")]
    [SerializeField, Min(0f)] private float knockbackTime = 0.2f;
    [Tooltip("Off for bosses / heavy enemies: hits never interrupt them.")]
    [SerializeField] private bool canBeStunned = true;
    [SerializeField, Min(0f)] private float hitStunTime = 0.25f;
    [Tooltip("After a stun ends, it can't be stunned again for this long (anti stun-lock).")]
    [SerializeField, Min(0f)] private float stunImmunityAfter = 0.3f;

    [Header("Visual")]
    [Tooltip("Empty = every SpriteRenderer in the children.")]
    [SerializeField] private SpriteRenderer[] flipRenderers;
    [Tooltip("Which way the sprite is DRAWN. Facing is done with flipX.")]
    [SerializeField] private bool spriteFacesRight = true;

    [Header("Start")]
    [Tooltip("Inactive and invulnerable until SetDormant(false) (boss arenas, ambushes).")]
    [SerializeField] private bool startDormant = false;

    [Header("Death")]
    [Tooltip("Seconds before the body is frozen. <= 0 = length of the Death animation.")]
    [SerializeField] private float deathDuration = -1f;
    [Tooltip("Seconds the body stays after dying, then it's destroyed. 0 = right away, < 0 = forever.")]
    [SerializeField] private float corpseLifetime = 3f;
    [Tooltip("Light (mana) the player gets when this dies.")]
    [SerializeField, Min(0f)] private float lightReward = 0f;
    [SerializeField] private UnityEvent onDeath;

    // ---- References ----
    public Health Health { get; private set; }
    public Rigidbody2D Rb { get; private set; }
    public EnemyAnimator Anim { get; private set; }
    public EnemyMotor Motor { get; private set; }
    public EnemyBrain Brain { get; private set; }
    public EnemyAttack[] Attacks { get; private set; } = new EnemyAttack[0];

    // ---- Info / stats ----
    public string DisplayName => displayName;
    public Faction Faction => Health.Faction;
    public float SpeedMultiplier { get => speedMultiplier; set => speedMultiplier = Mathf.Max(0f, value); }
    public float DamageMultiplier { get => damageMultiplier; set => damageMultiplier = Mathf.Max(0f, value); }
    public Vector2 SpawnPosition { get; private set; }
    public Vector2 Center => body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;

    // ---- State ----
    public bool IsDead { get; private set; }
    public bool IsDormant { get; private set; }
    public bool IsStunned => Time.time < stunUntil;
    public bool IsKnockedBack => Time.time < knockbackUntil;
    public int FacingSign { get; private set; } = 1;
    public bool IsAttacking { get { foreach (var a in Attacks) if (a != null && a.IsRunning) return true; return false; } }

    /// <summary>Motor may steer: not dead / dormant / stunned / knocked back / locked.</summary>
    public bool CanMove => !IsDead && !IsDormant && !IsStunned && !IsKnockedBack && movementLocks == 0;
    /// <summary>Brain may decide: not dead / dormant / stunned / AI-locked / mid-attack.</summary>
    public bool CanThink => !IsDead && !IsDormant && !IsStunned && aiLocks == 0 && !IsAttacking;

    // ---- Target (the player) ----
    public Transform Target => HasTarget ? PlayerController.Instance.transform : null;
    public bool HasTarget
    {
        get
        {
            var p = PlayerController.Instance;
            if (p == null) return false;
            if (targetOwner != p) CacheTarget(p);
            return targetHealth == null || targetHealth.IsAlive;
        }
    }
    public Health TargetHealth => HasTarget ? targetHealth : null;
    public Vector2 TargetCenter => targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)Target.position;
    public float DistanceToTarget => HasTarget ? Vector2.Distance(Center, TargetCenter) : float.PositiveInfinity;

    // ---- Events ----
    public event Action<Enemy> Died;
    public event Action<Enemy> Stunned;
    /// <summary>ResetToSpawn() was called (BossPhases listens to restart).</summary>
    public event Action ReturnedToSpawn;

    private Collider2D body;
    private PlayerController targetOwner;
    private Health targetHealth;
    private Collider2D targetCollider;

    private int movementLocks, aiLocks;
    private float stunUntil, knockbackUntil, stunImmuneUntil;
    private bool dormantInvulnerabilityHeld;
    private float originalDamping;

    // =====================  LIFECYCLE  =====================

    private void Awake()
    {
        Health = GetComponent<Health>();
        Rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        Anim = GetComponentInChildren<EnemyAnimator>();
        Motor = GetComponentInChildren<EnemyMotor>();
        Brain = GetComponentInChildren<EnemyBrain>();
        RefreshAttacks();

        Rb.freezeRotation = true;
        originalDamping = Rb.linearDamping;
        SpawnPosition = transform.position;

        if (Health.Faction == Faction.Neutral) Health.SetFaction(Faction.Enemy);
        if (flipRenderers == null || flipRenderers.Length == 0)
            flipRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void Start()
    {
        if (startDormant) SetDormant(true);
    }

    private void OnEnable()
    {
        Health.Damaged += OnDamaged;
        Health.Died += OnHealthDied;
    }

    private void OnDisable()
    {
        Health.Damaged -= OnDamaged;
        Health.Died -= OnHealthDied;
        if (dormantInvulnerabilityHeld) { dormantInvulnerabilityHeld = false; Health.UnlockInvulnerable(); }
    }

    /// <summary>Call after adding attacks at runtime.</summary>
    public void RefreshAttacks() => Attacks = GetComponentsInChildren<EnemyAttack>(true);

    // =====================  FACING  =====================

    public void Face(int sign)
    {
        if (sign == 0) return;
        FacingSign = sign > 0 ? 1 : -1;

        bool flip = spriteFacesRight ? FacingSign < 0 : FacingSign > 0;
        foreach (var r in flipRenderers) if (r != null) r.flipX = flip;
    }

    public void FaceTowards(float worldX)
    {
        float dx = worldX - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f) Face(dx > 0f ? 1 : -1);
    }

    // =====================  LOCKS  =====================

    /// <summary>Motor stops steering (attacks, scripted moves). Several systems can lock at once.</summary>
    public void LockMovement() => movementLocks++;
    public void UnlockMovement() => movementLocks = Mathf.Max(0, movementLocks - 1);
    /// <summary>Brain stops deciding (boss transitions, cutscenes). Running attacks are not stopped.</summary>
    public void LockAI() => aiLocks++;
    public void UnlockAI() => aiLocks = Mathf.Max(0, aiLocks - 1);

    public void StopHorizontal() { if (Rb != null) Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y); }

    // =====================  DORMANT / RESET  =====================

    /// <summary>Dormant = frozen, invulnerable, harmless. Wake it with SetDormant(false).</summary>
    public void SetDormant(bool dormant)
    {
        if (IsDead || IsDormant == dormant) return;
        IsDormant = dormant;

        if (dormant)
        {
            CancelAttacks();
            StopHorizontal();
            Motor?.Stop();
            if (!dormantInvulnerabilityHeld) { dormantInvulnerabilityHeld = true; Health.LockInvulnerable(); }
        }
        else if (dormantInvulnerabilityHeld)
        {
            dormantInvulnerabilityHeld = false;
            Health.UnlockInvulnerable();
        }
    }

    /// <summary>Back to start: full health, spawn position, brain reset. Not for dead enemies.</summary>
    public void ResetToSpawn()
    {
        if (IsDead) return;
        CancelAttacks();
        stunUntil = knockbackUntil = 0f;
        Rb.linearVelocity = Vector2.zero;
        transform.position = SpawnPosition;
        Health.Revive(1f);
        Motor?.Stop();
        Brain?.ResetBrain();
        Anim?.CancelOneShot();
        ReturnedToSpawn?.Invoke();
    }

    /// <summary>Makes it notice the player right now (alarms, boss start, summoned minions).</summary>
    public void Alert() => Brain?.Alert();

    // =====================  HIT REACTION  =====================

    private void OnDamaged(DamageInfo info)
    {
        if (IsDead || !Health.IsAlive) return;   // the killing blow is handled by OnHealthDied

        Brain?.OnHurt(info);

        // Stun first (it stops the enemy), THEN apply knockback so the push isn't cancelled
        if (canBeStunned && hitStunTime > 0f && Time.time >= stunImmuneUntil)
            Stun(hitStunTime);

        float push = 1f - knockbackResistance;
        if (info.knockback.sqrMagnitude > 0.0001f && push > 0f)
        {
            Rb.linearVelocity = info.knockback * push;
            knockbackUntil = Time.time + knockbackTime;
        }
    }

    /// <summary>Interrupts attacks and plays the Hurt animation.</summary>
    public void Stun(float duration)
    {
        if (IsDead || duration <= 0f) return;
        stunUntil = Time.time + duration;
        stunImmuneUntil = stunUntil + stunImmunityAfter;

        CancelAttacks();
        StopHorizontal();
        Motor?.Stop();
        Brain?.Interrupt();
        Anim?.PlayHurt(duration);
        Stunned?.Invoke(this);
    }

    public void CancelAttacks()
    {
        foreach (var a in Attacks) if (a != null && a.IsRunning) a.Cancel();
    }

    // =====================  DEATH  =====================

    private void OnHealthDied(DamageInfo info)
    {
        if (IsDead) return;
        IsDead = true;

        CancelAttacks();
        Motor?.Stop();
        if (Brain != null) Brain.enabled = false;
        if (Motor != null) Motor.enabled = false;
        Rb.linearDamping = Mathf.Max(originalDamping, 4f);   // killing blow slides to a stop

        Anim?.PlayDeath();

        if (lightReward > 0f && PlayerController.Instance != null)
            PlayerController.Instance.GetComponent<PlayerLight>()?.Add(lightReward);

        onDeath.Invoke();
        Died?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float wait = deathDuration > 0f ? deathDuration : (Anim != null ? Anim.GetLength(Anim.DeathState) : 0.5f);
        yield return new WaitForSeconds(wait);

        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = false;                       // body is frozen (and its colliders are off)

        if (corpseLifetime < 0f) yield break;       // stays forever
        yield return new WaitForSeconds(corpseLifetime);
        Destroy(gameObject);
    }

    // =====================  HELPERS  =====================

    private void CacheTarget(PlayerController p)
    {
        targetOwner = p;
        targetHealth = p.GetComponent<Health>();
        targetCollider = p.GetComponent<Collider2D>();
    }
}

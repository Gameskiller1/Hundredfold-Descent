using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Connects the player's Health to the player: hit knockback, death (animation + locks),
/// respawn at a checkpoint. Put it on the Player next to Health + PlayerController.
/// Health settings for the player: Invulnerable After Hit ~0.8s. Add HitFlash with flicker for feedback.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Hit Reaction")]
    [Tooltip("Seconds the player can't steer after being knocked back.")]
    [SerializeField, Min(0f)] private float knockbackLockTime = 0.2f;

    [Header("Death")]
    [Tooltip("Off = nothing happens after death except the PlayerDied event (you handle game over).")]
    [SerializeField] private bool autoRespawn = true;
    [SerializeField, Min(0f)] private float respawnDelay = 2f;

    public Health Health { get; private set; }
    public bool IsDead { get; private set; }
    public Vector2 Checkpoint { get; private set; }

    /// <summary>Static so anything (boss arenas, UI, music) can listen without a reference.</summary>
    public static event Action PlayerDied;
    public static event Action PlayerRespawned;

    private PlayerController player;
    private bool knockbackLocked;
    private bool deathLocks;
    private Coroutine knockbackRoutine;

    private void Awake()
    {
        Health = GetComponent<Health>();
        player = GetComponent<PlayerController>();
        Health.SetFaction(Faction.Player);
        Checkpoint = transform.position;
    }

    private void OnEnable()
    {
        Health.Damaged += OnDamaged;
        Health.Died += OnDied;
    }

    private void OnDisable()
    {
        Health.Damaged -= OnDamaged;
        Health.Died -= OnDied;
        ReleaseKnockback();
    }

    /// <summary>Where Respawn() puts the player. Call from checkpoints / save points.</summary>
    public void SetCheckpoint(Vector2 position) => Checkpoint = position;

    // =====================  HIT  =====================

    private void OnDamaged(DamageInfo info)
    {
        if (!Health.IsAlive || info.knockback.sqrMagnitude < 0.0001f) return;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(info.knockback));
    }

    private IEnumerator KnockbackRoutine(Vector2 velocity)
    {
        ReleaseKnockback();
        player.LockMovement();
        knockbackLocked = true;
        player.Rb.linearVelocity = velocity;

        yield return new WaitForSeconds(knockbackLockTime);

        knockbackRoutine = null;
        ReleaseKnockback();
    }

    private void ReleaseKnockback()
    {
        if (!knockbackLocked) return;
        knockbackLocked = false;
        player.UnlockMovement();
    }

    // =====================  DEATH  =====================

    private void OnDied(DamageInfo info)
    {
        if (IsDead) return;
        IsDead = true;

        if (knockbackRoutine != null) { StopCoroutine(knockbackRoutine); knockbackRoutine = null; }
        ReleaseKnockback();

        TargetedSpell.Active?.ForceCancel();   // never die while the game is frozen for aiming

        player.LockActions();
        player.LockMovement();
        deathLocks = true;
        player.StopHorizontal();
        player.Anim?.PlayDeath();

        PlayerDied?.Invoke();
        if (autoRespawn) StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    /// <summary>Revives at the checkpoint with full health.</summary>
    public void Respawn()
    {
        if (!IsDead) return;
        IsDead = false;

        transform.position = Checkpoint;
        player.StopImmediately();
        Health.Revive(1f);
        player.Anim?.Revive();

        if (deathLocks)
        {
            deathLocks = false;
            player.UnlockActions();
            player.UnlockMovement();
        }

        PlayerRespawned?.Invoke();
    }
}

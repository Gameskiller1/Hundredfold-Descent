using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Code-driven animation for enemies, same idea as PlayerAnimator: the Animator has NO transitions,
/// every animation is a state. State names must match the clip names exactly.
/// Locomotion (Idle / Move) is automatic from the real velocity.
/// Attacks, Hurt and Death are one-shots started by the other scripts.
/// Priority: Death > one-shot (attack / hurt / custom) > locomotion.
/// Any state you don't have is simply skipped, so you can add animations one by one.
/// Prefer your own Animator transitions? Turn "Auto Locomotion" off.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("State names (= Animator state AND clip names)")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Walk / Fly / Crawl... whatever this enemy does when moving.")]
    [SerializeField] private string moveState = "Walk";
    [SerializeField] private string hurtState = "Hurt";
    [SerializeField] private string deathState = "Death";

    [Header("Locomotion")]
    [SerializeField] private bool autoLocomotion = true;
    [Tooltip("Horizontal speed (units/sec) above which it counts as moving.")]
    [SerializeField, Min(0f)] private float moveThreshold = 0.1f;

    public string DeathState => deathState;
    public string Current { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsPlayingOneShot => oneShot != null;

    private Rigidbody2D rb;
    private readonly Dictionary<string, float> clipLengths = new Dictionary<string, float>();
    private readonly HashSet<string> warned = new HashSet<string>();
    private string oneShot;
    private float oneShotEnd;
    private bool forceReplay;

    private void Awake()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"[{name}] EnemyAnimator needs an Animator with a controller.", this);
            enabled = false;
            return;
        }

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            clipLengths[clip.name] = clip.length;
    }

    private void LateUpdate()
    {
        if (IsDead) return;
        if (oneShot != null && Time.time >= oneShotEnd) oneShot = null;

        string desired = oneShot;
        if (desired == null && autoLocomotion) desired = PickLocomotion();
        if (desired == null) return;

        if (desired != Current || forceReplay)
        {
            animator.Play(Animator.StringToHash(desired), 0, 0f);   // instant switch, no blending
            Current = desired;
            forceReplay = false;
        }
    }

    private string PickLocomotion()
    {
        bool moving = rb != null && Mathf.Abs(rb.linearVelocity.x) > moveThreshold;
        if (moving && Has(moveState)) return moveState;
        return Has(idleState) ? idleState : null;
    }

    // =====================  PUBLIC API  =====================

    /// <summary>Plays a state once, then returns to normal. duration &lt;= 0 = clip length. Missing state = ignored.</summary>
    public void Play(string state, float duration = -1f)
    {
        if (IsDead || !Has(state)) return;
        oneShot = state;
        oneShotEnd = Time.time + (duration > 0f ? duration : GetLength(state));
        forceReplay = true;
    }

    public void PlayHurt(float duration = -1f) => Play(hurtState, duration);

    /// <summary>Plays Death and blocks everything else until Revive().</summary>
    public void PlayDeath()
    {
        if (IsDead) return;
        IsDead = true;
        oneShot = null;
        if (Has(deathState))
        {
            animator.Play(Animator.StringToHash(deathState), 0, 0f);
            Current = deathState;
        }
    }

    public void Revive() { IsDead = false; oneShot = null; Current = null; }
    public void CancelOneShot() => oneShot = null;

    public bool Has(string state)
    {
        if (string.IsNullOrEmpty(state) || animator == null) return false;
        if (animator.HasState(0, Animator.StringToHash(state))) return true;
        if (warned.Add(state)) Debug.Log($"[{name}] Animator has no state '{state}' - skipped.", this);
        return false;
    }

    public float GetLength(string state)
    {
        if (!string.IsNullOrEmpty(state) && clipLengths.TryGetValue(state, out float len)) return len;
        return 0.3f;
    }
}

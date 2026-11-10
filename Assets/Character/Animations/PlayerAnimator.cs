
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Names must match the Animator state names AND clip names exactly.</summary>
public enum PlayerAnim { Idle, Idle2, Walk, Run, Jump, Fall, Land, Hide, Attack, Teleport, Death }

/// <summary>
/// Code-driven animation for pixel art. The Animator has NO transitions.
/// Locomotion (Idle/Idle2/Walk/Run/Jump/Fall/Land) is automatic.
/// Other scripts call PlayOneShot / SetHeld / PlayDeath to override it.
/// Priority: Death > One-shot > Held > Locomotion
/// Missing states fall back automatically (Fall -> Jump, Land -> Idle, Run -> Walk).
/// </summary>
[DisallowMultipleComponent]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Locomotion")]
    [Tooltip("Below this fraction of MoveSpeed = Idle.")]
    [SerializeField, Range(0f, 1f)] private float idleThreshold = 0.05f;
    [Tooltip("Off = always Walk when moving. Turn on once running exists.")]
    [SerializeField] private bool useRun = false;
    [Tooltip("Horizontal speed as a fraction of MoveSpeed. Above this = Run (only if Use Run).")]
    [SerializeField, Range(0f, 1f)] private float runThreshold = 0.6f;

    [Header("Air")]
    [Tooltip("Switch Jump -> Fall when vertical velocity drops below this. " +
             "Slightly above 0 makes Fall start a bit before the apex.")]
    [SerializeField] private float fallVelocityThreshold = 0.5f;

    [Header("Landing")]
    [Tooltip("Ignore tiny hops (e.g. stepping off small ledges) shorter than this airtime.")]
    [SerializeField] private float minAirTimeForLand = 0.15f;
    [Tooltip("Moving cancels the land animation immediately (feels more responsive).")]
    [SerializeField] private bool moveCancelsLand = true;

    [Header("Idle Blink (Idle2)")]
    [Tooltip("Random wait between blinks while standing still (seconds).")]
    [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 5f);

    // ---- Public state ----
    public PlayerAnim Current { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsPlayingOneShot => oneShot.HasValue;

    private PlayerController player;
    private static readonly int[] hashes = BuildHashes();                 // enum -> state hash
    private readonly Dictionary<PlayerAnim, float> clipLengths = new Dictionary<PlayerAnim, float>();
    private bool[] stateExists;                                            // enum -> exists in Animator

    private PlayerAnim? oneShot;          // Attack, Teleport...
    private float oneShotEnd;
    private Action onOneShotDone;
    private PlayerAnim? held;             // Hide (stays until cleared)
    private bool forceReplay;             // restart same anim (attack spam)

    // Idle blink
    private float nextBlinkTime = -1f;    // -1 = not idle / timer not started
    private float blinkEndTime;

    // Landing
    private bool wasGrounded = true;
    private float airStartTime;
    private float landEndTime;

    private void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Cache clip lengths by name so one-shots know when they end
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (Enum.TryParse(clip.name, out PlayerAnim a)) clipLengths[a] = clip.length;

        // Remember which states actually exist, so missing ones can fall back
        var values = (PlayerAnim[])Enum.GetValues(typeof(PlayerAnim));
        stateExists = new bool[values.Length];
        foreach (var v in values)
            stateExists[(int)v] = animator.HasState(0, hashes[(int)v]);
    }

    // LateUpdate: runs after the controller and abilities have updated this frame
    private void LateUpdate()
    {
        if (oneShot.HasValue && Time.time >= oneShotEnd) FinishOneShot();

        TrackLanding();

        PlayerAnim next = Resolve(PickState());

        // Left idle -> reset blink timer so it starts fresh next time we stand still
        if (next != PlayerAnim.Idle && next != PlayerAnim.Idle2)
        {
            nextBlinkTime = -1f;
            blinkEndTime = 0f;
        }

        if (next != Current || forceReplay)
        {
            animator.Play(hashes[(int)next], 0, 0f);   // instant switch, no blending
            Current = next;
            forceReplay = false;
        }
    }

    private void TrackLanding()
    {
        bool grounded = player.IsGrounded;

        if (wasGrounded && !grounded)
            airStartTime = Time.time;                                  // just left the ground

        if (!wasGrounded && grounded &&
            Time.time - airStartTime >= minAirTimeForLand &&
            stateExists[(int)PlayerAnim.Land])
            landEndTime = Time.time + GetLength(PlayerAnim.Land);      // just touched down

        wasGrounded = grounded;
    }

    private PlayerAnim PickState()
    {
        if (IsDead) return PlayerAnim.Death;
        if (oneShot.HasValue) return oneShot.Value;
        if (held.HasValue) return held.Value;

        // ---- Air ----
        if (!player.IsGrounded)
        {
            landEndTime = 0f;   // left the ground again (e.g. jumped during Land)
            return player.Rb.linearVelocity.y > fallVelocityThreshold ? PlayerAnim.Jump : PlayerAnim.Fall;
        }

        // ---- Ground ----
        // Uses real velocity, so dashes show movement and walking into a wall shows Idle
        float speed = Mathf.Abs(player.Rb.linearVelocity.x) / Mathf.Max(player.MoveSpeed, 0.01f);
        bool moving = speed >= idleThreshold;

        if (Time.time < landEndTime)
        {
            if (moving && moveCancelsLand) landEndTime = 0f;
            else return PlayerAnim.Land;
        }

        if (!moving) return PickIdle();
        return (useRun && speed >= runThreshold) ? PlayerAnim.Run : PlayerAnim.Walk;
    }

    /// <summary>Plain Idle most of the time, Idle2 (blink) once every few seconds.</summary>
    private PlayerAnim PickIdle()
    {
        float now = Time.time;

        if (now < blinkEndTime) return PlayerAnim.Idle2;          // blink still playing

        if (nextBlinkTime < 0f)                                    // just started idling
            nextBlinkTime = now + UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);

        if (now >= nextBlinkTime)                                  // time to blink
        {
            blinkEndTime = now + GetLength(PlayerAnim.Idle2);
            nextBlinkTime = blinkEndTime + UnityEngine.Random.Range(blinkInterval.x, blinkInterval.y);
            return PlayerAnim.Idle2;
        }

        return PlayerAnim.Idle;
    }

    /// <summary>If a state isn't in the Animator yet, use a sensible substitute.</summary>
    private PlayerAnim Resolve(PlayerAnim anim)
    {
        for (int i = 0; i < 4 && !stateExists[(int)anim]; i++)
        {
            switch (anim)
            {
                case PlayerAnim.Fall: anim = PlayerAnim.Jump; break;
                case PlayerAnim.Land: anim = PlayerAnim.Idle; break;
                case PlayerAnim.Run: anim = PlayerAnim.Walk; break;
                case PlayerAnim.Idle2: anim = PlayerAnim.Idle; break;
                default: return anim;   // no fallback: Unity will log the missing state
            }
        }
        return anim;
    }

    // =====================  PUBLIC API  =====================

    /// <summary>
    /// Plays an animation once, then returns to normal.
    /// onComplete fires when it finishes (NOT if interrupted or the player dies).
    /// duration &lt;= 0 uses the clip length.
    /// </summary>
    public void PlayOneShot(PlayerAnim anim, Action onComplete = null, float duration = -1f)
    {
        if (IsDead) return;
        oneShot = anim;
        oneShotEnd = Time.time + (duration > 0f ? duration : GetLength(anim));
        onOneShotDone = onComplete;
        forceReplay = true;
    }

    /// <summary>Stops the current one-shot without firing its callback.</summary>
    public void CancelOneShot() { oneShot = null; onOneShotDone = null; }

    /// <summary>Holds a looping animation (e.g. Hide) until ClearHeld() is called.</summary>
    public void SetHeld(PlayerAnim anim) => held = anim;
    public void ClearHeld() => held = null;

    /// <summary>Plays Death and blocks every other animation until Revive().</summary>
    public void PlayDeath()
    {
        IsDead = true;
        oneShot = null; onOneShotDone = null; held = null;
    }

    public void Revive() => IsDead = false;

    public float GetLength(PlayerAnim anim)
    {
        if (clipLengths.TryGetValue(anim, out float len)) return len;
        Debug.LogWarning($"[PlayerAnimator] No clip named '{anim}'. Using 0.3s.", this);
        return 0.3f;
    }

    // =====================  HELPERS  =====================

    private void FinishOneShot()
    {
        oneShot = null;
        var cb = onOneShotDone;
        onOneShotDone = null;
        cb?.Invoke();     // invoked last, so the callback may start another one-shot
    }

    private static int[] BuildHashes()
    {
        var names = Enum.GetNames(typeof(PlayerAnim));
        var result = new int[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = Animator.StringToHash(names[i]);
        return result;
    }
}

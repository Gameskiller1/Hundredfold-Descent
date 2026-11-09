using System.Collections;
using UnityEngine;

/// <summary>
/// Quick horizontal dash. Ignores gravity while dashing.
/// </summary>
public class DashAbility : PlayerAbility
{
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 14f;       // horizontal speed during dash
    [SerializeField] private float dashDuration = 0.12f;  // how long the dash lasts
    [SerializeField] private bool allowAirDash = true;    // false = ground only

    public bool IsDashing { get; private set; }

    // Can't dash while already dashing; optionally require ground (while we are on ground, dash's guaranteed
    // otherwise allowAirDash can pass true if it's true in the Inspector
    protected override bool CanActivate() => !IsDashing && (allowAirDash || player.IsGrounded);

    protected override void Activate() => StartCoroutine(DashRoutine());

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        player.LockMovement();  // controller stops writing velocity
        player.LockGravity();   // straight line, no falling

        // Dash towards input, or facing direction if no input
        float dirX = player.MoveInput != 0f ? Mathf.Sign(player.MoveInput) : player.FacingSign;
        player.Rb.linearVelocity = new Vector2(dirX * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);

        EndDash();
    }

    /// <summary>Restores everything the dash changed. Safe to call twice.</summary>
    private void EndDash()
    {
        if (!IsDashing) return;
        player.StopImmediately();
        player.UnlockGravity();
        player.UnlockMovement();
        IsDashing = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // If disabled mid-dash (death, scene change), don't leave locks stuck
        if (IsDashing) { StopAllCoroutines(); EndDash(); }
    }
}
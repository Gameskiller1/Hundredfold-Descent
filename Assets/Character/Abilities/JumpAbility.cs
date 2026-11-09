using UnityEngine;

/// <summary>
/// Platformer jump:
/// - Press = jump to jumpHeight
/// - Hold past holdThreshold = height grows up to maxHeightMultiplier
/// - Optional double jump if pressed again within doubleJumpWindow
/// </summary>
public class JumpAbility : PlayerAbility
{
    [Header("Jump")]
    [Tooltip("Base jump height in world units.")]
    [SerializeField] private float jumpHeight = 3f;

    [Header("Hold Boost")]
    [Tooltip("How long you must hold before the height starts growing.")]
    [SerializeField] private float holdThreshold = 0.3f;
    [Tooltip("Time from threshold until max height is reached.")]
    [SerializeField] private float boostRampTime = 0.25f;
    [Tooltip("1.2 = up to 20% higher when fully held.")]
    [SerializeField] private float maxHeightMultiplier = 1.2f;

    [Header("Double Jump")]
    [SerializeField] private bool enableDoubleJump = true;         // toggle (good for roguelike upgrades)
    [Tooltip("Second press must happen within this time after the first jump.")]
    [SerializeField] private float doubleJumpWindow = 0.3f;
    [SerializeField] private float doubleJumpHeight = 2f;          // extra height from the 2nd jump

    // ---- Public state (animations, UI, other abilities) ----
    public bool IsJumping { get; private set; }        // airborne because of a jump
    public bool HasDoubleJumped { get; private set; }  // 2nd jump used this airtime
    public bool EnableDoubleJump { get => enableDoubleJump; set => enableDoubleJump = value; }

    private float jumpStartTime;  // when the first jump started
    private float jumpStartY;     // Y position at take-off (for exact boost height)
    private bool boosting;        // can the hold boost still apply?

    protected override bool CanActivate()
    {
        if (player.IsMovementLocked) return false;            // no jumping mid-dash, cutscene, etc.
        if (player.IsGrounded && !IsJumping) return true;     // normal ground jump
        return CanDoubleJump();                               // otherwise only a double jump
    }

    private bool CanDoubleJump()
    {
        return enableDoubleJump
            && IsJumping
            && !HasDoubleJumped
            && Time.time - jumpStartTime <= doubleJumpWindow;
    }

    protected override void Activate()
    {
        if (player.IsGrounded && !IsJumping) GroundJump();
        else DoubleJump();
    }

    private void GroundJump()
    {
        IsJumping = true;
        HasDoubleJumped = false;
        boosting = true;                    // allow hold boost for this jump
        jumpStartTime = Time.time;
        jumpStartY = player.Rb.position.y;
        SetVerticalVelocity(VelocityForHeight(jumpHeight));
    }

    private void DoubleJump()
    {
        HasDoubleJumped = true;
        boosting = false;                   // double jump cancels the hold boost
        SetVerticalVelocity(VelocityForHeight(doubleJumpHeight));
    }

    private void FixedUpdate()
    {
        if (player == null) return;

        // Landed? (only once falling, and not on the same frames as take-off)
        if (IsJumping && player.IsGrounded && player.Rb.linearVelocity.y <= 0.01f
            && Time.time - jumpStartTime > 0.05f)
        {
            IsJumping = false;
            HasDoubleJumped = false;
            boosting = false;
        }

        ApplyHoldBoost();
    }

    /// <summary>While held past the threshold, raise the target peak up to maxHeightMultiplier.</summary>
    private void ApplyHoldBoost()
    {
        if (!boosting) return;

        bool held = inputAction != null && inputAction.action.IsPressed();
        float vy = player.Rb.linearVelocity.y;

        // Released or already falling -> boost is over
        if (!held || vy <= 0f) { boosting = false; return; }

        float heldTime = Time.time - jumpStartTime;
        if (heldTime < holdThreshold) return;  // not long enough yet

        // 0..1 progress of the ramp -> 1.0x .. 1.2x height
        float t = boostRampTime > 0f ? Mathf.Clamp01((heldTime - holdThreshold) / boostRampTime) : 1f;
        float targetPeakY = jumpStartY + jumpHeight * Mathf.Lerp(1f, maxHeightMultiplier, t);

        // Speed needed from the current position to reach the target peak
        float remaining = targetPeakY - player.Rb.position.y;
        float g = CurrentGravity();
        if (remaining > 0f && g > 0f)
        {
            float needed = Mathf.Sqrt(2f * g * remaining);
            if (needed > vy) SetVerticalVelocity(needed);  // only ever push up, never slow down
        }

        if (t >= 1f) boosting = false;  // max height reached -> stop boosting
    }

    // ---- Helpers ----

    // Physics formula: v = sqrt(2 * g * h)
    private float VelocityForHeight(float height) => Mathf.Sqrt(2f * CurrentGravity() * height);

    // Actual gravity on the player (0 if gravity is locked, e.g. during dash)
    private float CurrentGravity() => Mathf.Abs(Physics2D.gravity.y) * player.Rb.gravityScale;

    // Changes only Y, keeps horizontal movement
    private void SetVerticalVelocity(float vy) =>
        player.Rb.linearVelocity = new Vector2(player.Rb.linearVelocity.x, vy);
}
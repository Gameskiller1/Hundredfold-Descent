using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Side-view (platformer) player movement.
/// Handles horizontal input, gravity, ground detection and visuals.
/// Abilities control the player through the public API + lock system.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Vector2 'Move' action. Only X is used.")]
    [SerializeField] private InputActionReference moveAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;              // max horizontal speed
    [Tooltip("Higher = snappier. 0 = instant.")]
    [SerializeField] private float acceleration = 0f;           // units/sec² towards target speed

    [Header("Gravity")]
    [SerializeField] private float gravityScale = 3f;           // normal gravity strength
    [SerializeField] private float maxFallSpeed = 20f;          // terminal velocity

    [Header("Ground Check")]
    [Tooltip("Empty child placed at the player's feet.")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] private LayerMask groundLayer;             // what counts as ground

    [Header("Optional")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool persistBetweenScenes = true;

    // ---- Public API for abilities / other systems ----
    public static PlayerController Instance { get; private set; }   // global access (camera, UI...)
    public Rigidbody2D Rb { get; private set; }
    public float MoveInput { get; private set; }                    // -1..1 horizontal input
    public int FacingSign { get; private set; } = 1;                // 1 = right, -1 = left
    public Vector2 FacingDirection => new Vector2(FacingSign, 0f);  // kept for compatibility
    public bool IsGrounded { get; private set; }
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

    /// <summary>Abilities/cutscenes/camera transitions add a lock; movement resumes when all are released.</summary>
    public bool IsMovementLocked => movementLocks > 0;
    /// <summary>If false, abilities can't be activated (e.g., during camera transition).</summary>
    public bool CanAct => actionLocks == 0;
    /// <summary>While locked, gravity is disabled (dashes, ledge grabs, cutscenes...).</summary>
    public bool IsGravityLocked => gravityLocks > 0;

    // Reference counters - several systems can lock at once safely
    private int movementLocks;
    private int actionLocks;
    private int gravityLocks;

    private void Awake()
    {
        // Singleton: keep only one player across scenes
        if (persistBetweenScenes)
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }
        Instance = this;

        // Rigidbody setup for platformer
        Rb = GetComponent<Rigidbody2D>();
        Rb.gravityScale = gravityScale;
        Rb.freezeRotation = true;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // no tunneling on dashes

        // Only auto-find if not assigned in the Inspector
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnDestroy()
    {
        // Clear the static reference so it doesn't point at a destroyed object
        if (Instance == this) Instance = null;
    }

    private void OnEnable() { moveAction?.action.Enable(); }
    private void OnDisable() { moveAction?.action.Disable(); }

    private void Update()
    {
        // Read only the horizontal part of the move input
        float x = moveAction != null ? moveAction.action.ReadValue<Vector2>().x : 0f;
        MoveInput = Mathf.Abs(x) > 0.01f ? Mathf.Clamp(x, -1f, 1f) : 0f; // small deadzone

        // Update facing only when there is input (keeps last direction when idle)
        if (MoveInput != 0f) FacingSign = MoveInput > 0f ? 1 : -1;

        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        // These run even while movement is locked
        CheckGround();
        Rb.gravityScale = IsGravityLocked ? 0f : gravityScale;

        if (IsMovementLocked) return; // abilities control velocity while locked

        // Horizontal: go towards target speed (instantly or with acceleration)
        float targetX = MoveInput * moveSpeed;
        float newX = acceleration <= 0f ? targetX : Mathf.MoveTowards(Rb.linearVelocity.x, targetX, acceleration * Time.fixedDeltaTime);

        // Vertical: leave it to physics, just cap falling speed
        float newY = Mathf.Max(Rb.linearVelocity.y, -maxFallSpeed);

        Rb.linearVelocity = new Vector2(newX, newY);
    }

    /// <summary>Box overlap at the feet to detect ground.</summary>
    private void CheckGround()
    {
        IsGrounded = groundCheck != null && Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    private void UpdateVisuals()
    {
        // Flip sprite based on facing
        if (spriteRenderer != null)
            spriteRenderer.flipX = FacingSign < 0;

        // Animator parameters
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(MoveInput));
            animator.SetBool("Grounded", IsGrounded);
            animator.SetFloat("VelocityY", Rb.linearVelocity.y);
        }
    }

    // ---- Lock helpers ----
    public void LockMovement() => movementLocks++;
    public void UnlockMovement() => movementLocks = Mathf.Max(0, movementLocks - 1);
    public void LockActions() => actionLocks++;
    public void UnlockActions() => actionLocks = Mathf.Max(0, actionLocks - 1);
    public void LockGravity() => gravityLocks++;
    public void UnlockGravity() => gravityLocks = Mathf.Max(0, gravityLocks - 1);

    // ---- Velocity helpers ----
    /// <summary>Kills all velocity.</summary>
    public void StopImmediately() => Rb.linearVelocity = Vector2.zero;
    /// <summary>Kills only horizontal velocity (keeps falling/jumping).</summary>
    public void StopHorizontal() => Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);

    // Draws the ground check box in the Scene view
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
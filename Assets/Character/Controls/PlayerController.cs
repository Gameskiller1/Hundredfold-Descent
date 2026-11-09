using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Higher = snappier. 0 = instant.")]
    [SerializeField] private float acceleration = 0f;

    [Header("Optional")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool persistBetweenScenes = true;

    // ---- Public API for abilities / other systems ----
    public Rigidbody2D Rb { get; private set; }
    public Vector2 MoveInput { get; private set; }
    public Vector2 FacingDirection { get; private set; } = Vector2.down;
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

    /// <summary>Abilities/cutscenes/camera transitions add a lock; movement resumes when all are released.</summary>
    public bool IsMovementLocked => movementLocks > 0;
    /// <summary>If false, abilities can't be activated (e.g., during camera transition).</summary>
    public bool CanAct => actionLocks == 0;

    private int movementLocks;
    private int actionLocks;

    private static PlayerController instance;

    private void Awake()
    {
        if (persistBetweenScenes)
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        Rb = GetComponent<Rigidbody2D>();
        Rb.gravityScale = 0f;
        Rb.freezeRotation = true;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() { moveAction?.action.Enable(); }
    private void OnDisable() { moveAction?.action.Disable(); }

    private void Update()
    {
        MoveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (MoveInput.sqrMagnitude > 1f) MoveInput = MoveInput.normalized;

        if (MoveInput.sqrMagnitude > 0.01f)
            FacingDirection = MoveInput.normalized;

        UpdateVisuals();
    }

    private void FixedUpdate()
    {
        if (IsMovementLocked) return; // abilities control velocity while locked

        Vector2 target = MoveInput * moveSpeed;
        Rb.linearVelocity = acceleration <= 0f ? target : Vector2.MoveTowards(Rb.linearVelocity, target, acceleration * Time.fixedDeltaTime);
    }

    private void UpdateVisuals()
    {
        if (spriteRenderer != null && Mathf.Abs(MoveInput.x) > 0.01f)
            spriteRenderer.flipX = MoveInput.x < 0f;

        if (animator != null)
        {
            animator.SetFloat("MoveX", FacingDirection.x);
            animator.SetFloat("MoveY", FacingDirection.y);
            animator.SetFloat("Speed", MoveInput.sqrMagnitude);
        }
    }

    // ---- Lock helpers ----
    public void LockMovement() => movementLocks++;
    public void UnlockMovement() => movementLocks = Mathf.Max(0, movementLocks - 1);
    public void LockActions() => actionLocks++;
    public void UnlockActions() => actionLocks = Mathf.Max(0, actionLocks - 1);

    public void StopImmediately() => Rb.linearVelocity = Vector2.zero;
}
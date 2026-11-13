using UnityEngine;

/// <summary>Flying enemy: moves freely in 2D (no gravity), with an optional idle hover bob.</summary>
public class FlyMotor : EnemyMotor
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [Tooltip("Units/sec². Higher = snappier.")]
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float stopDistance = 0.15f;

    [Header("Hover")]
    [SerializeField] private bool hover = true;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.2f;
    [SerializeField, Min(0f)] private float hoverFrequency = 1.2f;

    private Vector2 target;
    private bool hasTarget;
    private float hoverPhase;

    protected override void Awake()
    {
        base.Awake();
        rb.gravityScale = 0f;
    }

    public override void MoveToward(Vector2 point) { target = point; hasTarget = true; }
    public override void Stop() => hasTarget = false;

    private void FixedUpdate()
    {
        if (!CanMove) return;

        Vector2 desired = Vector2.zero;
        if (hasTarget)
        {
            Vector2 to = target - rb.position;
            if (to.magnitude > stopDistance) desired = to.normalized * moveSpeed * enemy.SpeedMultiplier;
        }

        Vector2 v = Vector2.MoveTowards(rb.linearVelocity, desired, acceleration * Time.fixedDeltaTime);

        if (hover && desired == Vector2.zero)
        {
            hoverPhase += Time.fixedDeltaTime * hoverFrequency * Mathf.PI * 2f;
            v.y = Mathf.Cos(hoverPhase) * hoverAmplitude * hoverFrequency * Mathf.PI * 2f;
        }

        rb.linearVelocity = v;
        if (Mathf.Abs(desired.x) > 0.05f) enemy.Face(desired.x > 0f ? 1 : -1);
    }
}

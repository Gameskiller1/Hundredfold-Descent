using UnityEngine;

/// <summary>Ground walker: moves horizontally, respects gravity, optionally stops at ledges and walls.</summary>
public class WalkMotor : EnemyMotor
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [Tooltip("Units/sec². 0 = instant.")]
    [SerializeField, Min(0f)] private float acceleration = 0f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 20f;
    [Tooltip("Within this horizontal distance of the target it stops (prevents jitter).")]
    [SerializeField, Min(0f)] private float stopDistance = 0.1f;

    [Header("Obstacles")]
    [Tooltip("Same layer(s) as the level's ground / walls.")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool stopAtLedges = true;
    [SerializeField, Min(0f)] private float ledgeCheckDistance = 0.2f;
    [SerializeField, Min(0.1f)] private float ledgeDropDepth = 0.6f;
    [SerializeField] private bool stopAtWalls = true;
    [SerializeField, Min(0f)] private float wallCheckDistance = 0.1f;

    public override bool IsBlocked => blocked;

    private Collider2D body;
    private float desiredDir;
    private bool blocked;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponentInParent<Collider2D>();
    }

    public override void MoveToward(Vector2 point)
    {
        float dx = point.x - rb.position.x;
        desiredDir = Mathf.Abs(dx) <= stopDistance ? 0f : Mathf.Sign(dx);
    }

    public override void Stop() => desiredDir = 0f;

    private void FixedUpdate()
    {
        if (!CanMove) return;

        float dir = desiredDir;
        blocked = false;

        if (dir != 0f)
        {
            enemy.Face((int)dir);
            if (HitsWall(dir) || (stopAtLedges && LedgeAhead(dir))) { blocked = true; dir = 0f; }
        }

        float target = dir * moveSpeed * enemy.SpeedMultiplier;
        Vector2 v = rb.linearVelocity;
        float x = acceleration <= 0f ? target : Mathf.MoveTowards(v.x, target, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(x, Mathf.Max(v.y, -maxFallSpeed));
    }

    private bool HitsWall(float dir)
    {
        if (!stopAtWalls || body == null) return false;
        Bounds b = body.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y + b.size.y * 0.3f);
        return Physics2D.Raycast(origin, new Vector2(dir, 0f), b.extents.x + wallCheckDistance, groundLayer).collider != null;
    }

    private bool LedgeAhead(float dir)
    {
        if (body == null) return false;
        Bounds b = body.bounds;
        Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + ledgeCheckDistance), b.min.y + 0.05f);

        // Only matters while standing on something (don't "see" a ledge mid-fall)
        bool grounded = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, groundLayer).collider != null;
        if (!grounded) return false;

        return Physics2D.Raycast(origin, Vector2.down, ledgeDropDepth + 0.05f, groundLayer).collider == null;
    }

    private void OnDrawGizmosSelected()
    {
        var c = body != null ? body : GetComponentInParent<Collider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        Gizmos.color = Color.cyan;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 o = new Vector2(b.center.x + s * (b.extents.x + ledgeCheckDistance), b.min.y + 0.05f);
            Gizmos.DrawLine(o, o + Vector2.down * (ledgeDropDepth + 0.05f));
        }
    }
}

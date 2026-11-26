using UnityEngine;

/// <summary>
/// HOW an enemy moves. Brains only say "go toward this point" / "stop"; the motor does it.
/// Make a new movement type (jumper, wall-crawler, teleporter...) by inheriting this.
/// </summary>
public abstract class EnemyMotor : MonoBehaviour
{
    protected Enemy enemy;
    protected Rigidbody2D rb;

    protected virtual void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        rb = GetComponentInParent<Rigidbody2D>();
    }

    /// <summary>Keep heading toward this world point until told otherwise.</summary>
    public abstract void MoveToward(Vector2 point);

    public abstract void Stop();

    /// <summary>A wall / ledge stopped it this frame (patrols turn around on this).</summary>
    public virtual bool IsBlocked => false;

    protected bool CanMove => enemy != null && enemy.CanMove;
}

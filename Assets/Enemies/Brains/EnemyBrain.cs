using UnityEngine;

/// <summary>
/// WHAT an enemy decides to do. One brain per enemy (on the root or a child).
/// Tick() runs every frame while the enemy is free to act (alive, awake, not stunned, not mid-attack).
/// The ready-made brain is PatrolChaseBrain. Write your own for special enemies/bosses:
/// inherit, implement Tick(), use enemy.Motor and enemy.Attacks.
/// </summary>
public abstract class EnemyBrain : MonoBehaviour
{
    protected Enemy enemy;

    protected virtual void Awake() => enemy = GetComponentInParent<Enemy>();

    protected virtual void Start() { }

    private void Update()
    {
        if (enemy != null && enemy.CanThink) Tick();
    }

    protected abstract void Tick();

    /// <summary>The enemy took a hit (not the killing one).</summary>
    public virtual void OnHurt(DamageInfo info) { }

    /// <summary>Stunned: drop whatever you were doing.</summary>
    public virtual void Interrupt() { }

    /// <summary>Force the enemy to notice the player.</summary>
    public virtual void Alert() { }

    /// <summary>Back to the starting state (boss reset).</summary>
    public virtual void ResetBrain() { }
}

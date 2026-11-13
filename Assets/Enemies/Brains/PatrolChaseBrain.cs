using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The standard enemy brain: patrol around its spawn -> notice the player -> chase -> use attacks
/// -> lose the player -> go back to patrol. Works with any motor (WalkMotor, FlyMotor...) and any attacks.
/// Bosses use it too: BossPhases changes which attacks are enabled and the speed/damage multipliers.
/// </summary>
public class PatrolChaseBrain : EnemyBrain
{
    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRange = 6f;
    [Tooltip("Once alerted, it keeps tracking until you're further than this.")]
    [SerializeField, Min(0f)] private float loseTargetRange = 9f;
    [Tooltip("Ignore the player above/below this height difference (other floors).")]
    [SerializeField, Min(0f)] private float verticalRange = 3f;
    [Tooltip("Seconds it keeps hunting without seeing the player before giving up.")]
    [SerializeField, Min(0f)] private float loseTargetDelay = 2f;
    [Tooltip("Off = can't notice you from behind (until alerted).")]
    [SerializeField] private bool seesBehind = false;
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Layers that block sight (Ground).")]
    [SerializeField] private LayerMask sightBlockers;
    [SerializeField] private Vector2 eyeOffset = new Vector2(0.3f, 0.5f);
    [Tooltip("Getting hit alerts it instantly.")]
    [SerializeField] private bool alertWhenHurt = true;

    [Header("Patrol")]
    [Tooltip("Off = stands still until it sees you (statues, ambushers).")]
    [SerializeField] private bool patrol = true;
    [SerializeField, Min(0f)] private float patrolDistance = 3f;
    [SerializeField, Min(0f)] private float waitAtEnds = 1f;

    [Header("Chase")]
    [Tooltip("Stops this close to the player (set near the attack range).")]
    [SerializeField, Min(0f)] private float approachDistance = 1f;
    [Tooltip("Ranged enemies: backs away when the player is closer than this. 0 = never.")]
    [SerializeField, Min(0f)] private float retreatDistance = 0f;

    public bool IsAlerted { get; private set; }

    private Vector2 leftPoint, rightPoint;
    private int patrolDir = 1;
    private float waitUntil;
    private float lastSeenTime;
    private Vector2 lastKnownPos;
    private readonly List<EnemyAttack> candidates = new List<EnemyAttack>();

    protected override void Start()
    {
        base.Start();
        Vector2 home = enemy.SpawnPosition;
        leftPoint = home + Vector2.left * patrolDistance;
        rightPoint = home + Vector2.right * patrolDistance;
    }

    protected override void Tick()
    {
        bool sees = CanSee();
        if (sees)
        {
            lastSeenTime = Time.time;
            lastKnownPos = enemy.TargetCenter;
            if (!IsAlerted) Alert();
        }

        if (IsAlerted)
        {
            bool gone = !enemy.HasTarget
                        || enemy.DistanceToTarget > loseTargetRange
                        || Time.time - lastSeenTime > loseTargetDelay;
            if (gone) { LoseTarget(); }
        }

        if (IsAlerted) CombatTick(sees);
        else PatrolTick();
    }

    // =====================  COMBAT  =====================

    private void CombatTick(bool sees)
    {
        Vector2 pos = enemy.Center;

        if (sees)
        {
            EnemyAttack attack = PickAttack(enemy.TargetCenter);
            if (attack != null)
            {
                enemy.Motor?.Stop();
                enemy.FaceTowards(enemy.TargetCenter.x);
                attack.Begin();
                return;
            }
        }

        // Without sight it walks to where it last saw you
        Vector2 goal = sees ? enemy.TargetCenter : lastKnownPos;
        float dist = Vector2.Distance(pos, goal);

        if (sees && retreatDistance > 0f && dist < retreatDistance)
        {
            float away = pos.x >= goal.x ? 1f : -1f;
            enemy.Motor?.MoveToward(pos + new Vector2(away * 5f, 0f));
        }
        else if (dist > approachDistance)
        {
            enemy.Motor?.MoveToward(goal);
        }
        else
        {
            enemy.Motor?.Stop();
            enemy.FaceTowards(goal.x);
        }
    }

    /// <summary>Weighted random among attacks that are enabled, off cooldown and in range.</summary>
    private EnemyAttack PickAttack(Vector2 targetCenter)
    {
        candidates.Clear();
        float total = 0f;
        foreach (var a in enemy.Attacks)
        {
            if (a == null || !a.CanStart(targetCenter)) continue;
            candidates.Add(a);
            total += a.Weight;
        }
        if (candidates.Count == 0) return null;

        float roll = Random.value * total;
        foreach (var a in candidates)
        {
            roll -= a.Weight;
            if (roll <= 0f) return a;
        }
        return candidates[candidates.Count - 1];
    }

    // =====================  PATROL  =====================

    private void PatrolTick()
    {
        if (!patrol) { enemy.Motor?.Stop(); return; }

        if (Time.time < waitUntil) { enemy.Motor?.Stop(); return; }

        Vector2 point = patrolDir > 0 ? rightPoint : leftPoint;
        enemy.Motor?.MoveToward(point);

        bool arrived = Mathf.Abs(enemy.transform.position.x - point.x) <= 0.2f;
        bool blocked = enemy.Motor != null && enemy.Motor.IsBlocked;
        if (arrived || blocked)
        {
            patrolDir = -patrolDir;
            waitUntil = Time.time + waitAtEnds;
            enemy.Motor?.Stop();
        }
    }

    // =====================  SENSING  =====================

    private bool CanSee()
    {
        if (!enemy.HasTarget) return false;

        Vector2 eye = enemy.Center + new Vector2(eyeOffset.x * enemy.FacingSign, eyeOffset.y);
        Vector2 target = enemy.TargetCenter;

        float range = IsAlerted ? loseTargetRange : detectionRange;
        if (Vector2.Distance(eye, target) > range) return false;
        if (Mathf.Abs(target.y - eye.y) > verticalRange) return false;

        if (!seesBehind && !IsAlerted)
        {
            float side = target.x - enemy.transform.position.x;
            if (Mathf.Abs(side) > 0.1f && Mathf.Sign(side) != enemy.FacingSign) return false;
        }

        if (requireLineOfSight && Physics2D.Linecast(eye, target, sightBlockers).collider != null) return false;
        return true;
    }

    // =====================  EVENTS  =====================

    public override void Alert()
    {
        IsAlerted = true;
        lastSeenTime = Time.time;
        if (enemy != null && enemy.HasTarget) lastKnownPos = enemy.TargetCenter;
    }

    private void LoseTarget()
    {
        IsAlerted = false;
        waitUntil = Time.time + waitAtEnds;
        enemy.Motor?.Stop();
    }

    public override void OnHurt(DamageInfo info)
    {
        if (alertWhenHurt) Alert();
    }

    public override void Interrupt() => enemy.Motor?.Stop();

    public override void ResetBrain()
    {
        IsAlerted = false;
        patrolDir = 1;
        waitUntil = 0f;
        enemy.Motor?.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position;
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(c, detectionRange);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f); Gizmos.DrawWireSphere(c, loseTargetRange);

        if (!patrol) return;
        Vector3 home = Application.isPlaying && enemy != null ? (Vector3)enemy.SpawnPosition : c;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(home + Vector3.left * patrolDistance, home + Vector3.right * patrolDistance);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Telegraphs, then charges / leaps horizontally and hurts whatever it touches on the way.</summary>
public class LungeEnemyAttack : EnemyAttack
{
    [Header("Timing (seconds)")]
    [Tooltip("Telegraph before the charge (crouch, roar...).")]
    [SerializeField, Min(0f)] private float windup = 0.5f;
    [SerializeField, Min(0.05f)] private float lungeDuration = 0.35f;
    [SerializeField, Min(0f)] private float recovery = 0.6f;

    [Header("Lunge")]
    [SerializeField, Min(0f)] private float lungeSpeed = 10f;
    [Tooltip("Small upward hop at the start (0 = flat charge).")]
    [SerializeField, Min(0f)] private float hopSpeed = 0f;
    [Tooltip("Re-aim at the player right before charging (off = commits to the direction it faced).")]
    [SerializeField] private bool retargetAfterWindup = false;

    [Header("Hit Box (moves with the enemy)")]
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1f, 1f);
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.2f, 0.5f);
    [SerializeField] private AttackDamage damage = new AttackDamage();

    private readonly HashSet<Health> hitThisLunge = new HashSet<Health>();

    protected override IEnumerator Execute()
    {
        hitThisLunge.Clear();
        PlayAnimation(windup + lungeDuration + recovery);

        yield return Wait(windup);

        if (retargetAfterWindup && enemy.HasTarget) enemy.FaceTowards(enemy.TargetCenter.x);
        int dir = enemy.FacingSign;

        float t = 0f;
        while (t < lungeDuration)
        {
            float y = (hopSpeed > 0f && t <= 0f) ? hopSpeed : enemy.Rb.linearVelocity.y;
            enemy.Rb.linearVelocity = new Vector2(dir * lungeSpeed * enemy.SpeedMultiplier, y);
            HitBox(Local(hitBoxOffset), hitBoxSize, damage, hitThisLunge);

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        enemy.StopHorizontal();
        yield return Wait(recovery);
    }

    protected override void OnCancelled() => enemy.StopHorizontal();

    private void OnDrawGizmosSelected()
    {
        var e = GetComponentInParent<Enemy>();
        int facing = e != null ? e.FacingSign : 1;
        Vector3 root = e != null ? e.transform.position : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(root + new Vector3(hitBoxOffset.x * facing, hitBoxOffset.y), hitBoxSize);
    }
}

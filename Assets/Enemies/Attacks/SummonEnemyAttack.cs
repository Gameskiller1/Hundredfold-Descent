using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Calls in minions (any enemy prefab). Mostly for bosses. Limits how many can be alive at once.</summary>
public class SummonEnemyAttack : EnemyAttack
{
    [Header("Summon")]
    [SerializeField] private Enemy minionPrefab;
    [Tooltip("Spawn positions relative to the summoner. X is mirrored by facing.")]
    [SerializeField] private Vector2[] spawnOffsets = { new Vector2(-2f, 0f), new Vector2(2f, 0f) };
    [Tooltip("Won't summon while this many minions are alive.")]
    [SerializeField, Min(1)] private int maxAlive = 4;
    [Tooltip("Minions start already hunting the player.")]
    [SerializeField] private bool alertMinions = true;
    [Tooltip("Minions die with the summoner (boss death).")]
    [SerializeField] private bool killMinionsOnDeath = true;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0f)] private float windup = 1f;
    [SerializeField, Min(0f)] private float recovery = 0.8f;

    private readonly List<Enemy> minions = new List<Enemy>();

    protected override void Awake()
    {
        base.Awake();
        if (enemy != null) enemy.Died += OnSummonerDied;
    }

    private void OnDestroy()
    {
        if (enemy != null) enemy.Died -= OnSummonerDied;
    }

    protected override bool IsUsable()
    {
        minions.RemoveAll(m => m == null || m.IsDead);
        return minionPrefab != null && minions.Count < maxAlive;
    }

    protected override IEnumerator Execute()
    {
        PlayAnimation(windup + recovery);
        yield return Wait(windup);

        foreach (var offset in spawnOffsets)
        {
            if (minions.Count >= maxAlive) break;
            var m = Instantiate(minionPrefab, Local(offset), Quaternion.identity);
            minions.Add(m);
            if (alertMinions) m.Alert();
        }

        yield return Wait(recovery);
    }

    private void OnSummonerDied(Enemy _)
    {
        if (!killMinionsOnDeath) return;
        foreach (var m in minions)
            if (m != null && !m.IsDead) m.Health.Kill();
    }
}

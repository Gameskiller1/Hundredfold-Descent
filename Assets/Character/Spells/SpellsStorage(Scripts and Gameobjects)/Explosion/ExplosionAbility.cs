
using UnityEngine;

/// <summary>X -> aim -> LMB detonates an explosion on the ground under the cursor, Q cancels.</summary>
public class ExplosionAbility : TargetedSpell
{
    [Header("Explosion")]
    [SerializeField] private Explosion explosionPrefab;

    private Explosion explosionInstance;

    protected override void Cast(Vector2 point)
    {
        var explosion = GetOrSpawn(explosionPrefab, ref explosionInstance);
        if (explosion == null) { Debug.LogWarning("[Explosion] No explosion prefab assigned.", this); return; }
        explosion.Play(point);
    }
}

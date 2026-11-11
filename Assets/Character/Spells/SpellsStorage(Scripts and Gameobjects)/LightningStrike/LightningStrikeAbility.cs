
using UnityEngine;

/// <summary>Z -> aim -> LMB strikes lightning at the ground under the cursor, Q cancels.</summary>
public class LightningStrikeAbility : TargetedSpell
{
    [Header("Lightning")]
    [SerializeField] private LightningStrike strikePrefab;

    private LightningStrike strikeInstance;

    protected override void Cast(Vector2 point)
    {
        var strike = GetOrSpawn(strikePrefab, ref strikeInstance);
        if (strike == null) { Debug.LogWarning("[Lightning] No strike prefab assigned.", this); return; }
        strike.Play(point);
    }
}

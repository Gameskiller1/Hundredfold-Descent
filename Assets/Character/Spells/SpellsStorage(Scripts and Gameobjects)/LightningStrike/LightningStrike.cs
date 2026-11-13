
using System.Collections;
using UnityEngine;

/// <summary>Goes on the ROOT of the lightning prefab. Child visual is toggled automatically.</summary>
public class LightningStrike : SpellEffect
{
    [Header("Damage")]
    [SerializeField] private bool dealDamage = false;
    [SerializeField, Min(0f)] private float damage = 30f;
    [SerializeField] private DamageType damageType = DamageType.Lightning;
    [Tooltip("Seconds after the strike starts until the hit (match the impact frame).")]
    [SerializeField] private float hitDelay = 0.1f;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1f, 3f);
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0f, 1.5f);
    [SerializeField] private LayerMask hitLayers;

    protected override IEnumerator PlayRoutine(float length)
    {
        if (!dealDamage)
        {
            yield return new WaitForSeconds(length);
            yield break;
        }

        float delay = Mathf.Min(hitDelay, length);
        yield return new WaitForSeconds(delay);
        DoHit();
        yield return new WaitForSeconds(length - delay);
    }

    private void DoHit()
    {
        Vector2 center = (Vector2)transform.position + hitBoxOffset;
        foreach (var t in DamageUtil.Box(center, hitBoxSize, 0f, hitLayers))
        {
            var info = new DamageInfo(damage, Faction.Player, gameObject, damageType);
            info.point = t.Collider.bounds.center;
            t.Damageable.TakeDamage(info);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!dealDamage) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube((Vector2)transform.position + hitBoxOffset, hitBoxSize);
    }
}

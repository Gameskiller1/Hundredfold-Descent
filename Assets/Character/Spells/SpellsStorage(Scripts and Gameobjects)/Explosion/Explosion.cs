
using System.Collections;
using UnityEngine;

/// <summary>Goes on the ROOT of the explosion prefab. Child visual is toggled automatically.</summary>
public class Explosion : SpellEffect
{
    [Header("Damage")]
    [SerializeField] private bool dealDamage = false;
    [SerializeField, Min(0f)] private float damage = 25f;
    [SerializeField] private DamageType damageType = DamageType.Explosion;
    [Tooltip("Seconds after the explosion starts until the blast (match the impact frame).")]
    [SerializeField] private float hitDelay = 0.4f;
    [SerializeField, Min(0f)] private float radius = 1.5f;
    [SerializeField] private Vector2 hitOffset = new Vector2(0f, 0.75f);
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("Damage/knockback multiplier at the edge of the radius (1 = no falloff).")]
    [SerializeField, Range(0f, 1f)] private float edgeMultiplier = 0.5f;

    [Header("Knockback")]
    [Tooltip("Impulse pushed away from the center. 0 = off.")]
    [SerializeField, Min(0f)] private float knockbackForce = 8f;
    [Tooltip("Extra upward push so things get launched instead of slid along the floor.")]
    [SerializeField, Min(0f)] private float upwardBias = 0.5f;

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
        Vector2 center = (Vector2)transform.position + hitOffset;

        // damageablesOnly: false -> loose physics props (crates...) get shoved too
        foreach (var t in DamageUtil.Circle(center, radius, hitLayers, damageablesOnly: false))
        {
            Vector2 at = t.Collider.bounds.center;
            float dist = Vector2.Distance(center, t.Collider.ClosestPoint(center));
            float multiplier = Mathf.Lerp(1f, edgeMultiplier, radius > 0f ? Mathf.Clamp01(dist / radius) : 0f);

            Vector2 dir = at - center;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir = (dir.normalized + Vector2.up * upwardBias).normalized;
            Vector2 push = dir * knockbackForce * multiplier;

            if (t.Damageable != null)
            {
                // Enemies handle their own knockback from the hit info
                var info = new DamageInfo(damage * multiplier, Faction.Player, gameObject, damageType);
                info.point = at;
                info.knockback = push;
                t.Damageable.TakeDamage(info);
            }
            else
            {
                var rb = t.Collider.attachedRigidbody;
                if (knockbackForce > 0f && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                    rb.AddForce(push, ForceMode2D.Impulse);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!dealDamage) return;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere((Vector2)transform.position + hitOffset, radius);
    }
}

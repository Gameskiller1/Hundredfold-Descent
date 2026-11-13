
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Goes on the ROOT of the explosion prefab. Child visual is toggled automatically.</summary>
public class Explosion : SpellEffect
{
    [Header("Damage")]
    [SerializeField] private bool dealDamage = false;
    [SerializeField, Min(0f)] private float damage = 25f;
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

    private readonly HashSet<Object> alreadyHit = new HashSet<Object>();

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
        alreadyHit.Clear();

        foreach (var h in Physics2D.OverlapCircleAll(center, radius, hitLayers))
        {
            // One hit per object, even if it has several colliders
            Object key = h.attachedRigidbody != null ? h.attachedRigidbody : (Object)h.transform.root;
            if (!alreadyHit.Add(key)) continue;

            Vector2 closest = h.ClosestPoint(center);
            float t = radius > 0f ? Mathf.Clamp01(Vector2.Distance(center, closest) / radius) : 0f;
            float multiplier = Mathf.Lerp(1f, edgeMultiplier, t);

            // TODO: your health system, e.g. h.GetComponentInParent<Health>()?.TakeDamage(damage * multiplier);
            Debug.Log($"[Explosion] Hit {h.name} for {damage * multiplier:0.#}", h);

            ApplyKnockback(h, center, multiplier);
        }
    }

    private void ApplyKnockback(Collider2D h, Vector2 center, float multiplier)
    {
        var rb = h.attachedRigidbody;
        if (knockbackForce <= 0f || rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;

        Vector2 dir = (Vector2)h.bounds.center - center;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir = (dir.normalized + Vector2.up * upwardBias).normalized;

        rb.AddForce(dir * knockbackForce * multiplier, ForceMode2D.Impulse);
    }

    private void OnDrawGizmosSelected()
    {
        if (!dealDamage) return;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere((Vector2)transform.position + hitOffset, radius);
    }
}

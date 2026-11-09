using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Press the button to interact with the nearest Interactable in range.
/// </summary>
public class InteractAbility : PlayerAbility
{
    [Header("Interact")]
    [SerializeField] private float radius = 1.2f;            // reach
    [SerializeField] private LayerMask interactableLayer;    // set to "Interactable"
    [SerializeField] private Vector2 offset = Vector2.zero;  // e.g. (0, 0.5) to center on body

    /// <summary>Nearest valid interactable right now (for "Press E" UI prompts). Null if none.</summary>
    public Interactable CurrentTarget { get; private set; }

    private readonly List<Collider2D> hits = new List<Collider2D>(); // reused, no garbage
    private ContactFilter2D filter;

    // Small cooldown so one press can't trigger twice
    private void Reset() => cooldown = 0.2f;

    protected override void Awake()
    {
        base.Awake();
        filter = new ContactFilter2D();
        filter.SetLayerMask(interactableLayer);
        filter.useTriggers = true; // torches use trigger colliders
    }

    private void Update()
    {
        // Updated every frame so UI can show a prompt over CurrentTarget
        CurrentTarget = FindNearest();
    }

    protected override bool CanActivate() => CurrentTarget != null;

    protected override void Activate() => CurrentTarget.Interact(player);

    private Interactable FindNearest()
    {
        Vector2 center = (Vector2)transform.position + offset;
        Physics2D.OverlapCircle(center, radius, filter, hits);

        Interactable best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // GetComponentInParent: the collider can be on a child of the torch
            var interactable = hit.GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.CanInteract(player)) continue;

            float dist = ((Vector2)hit.transform.position - center).sqrMagnitude; // sqr = cheaper
            if (dist < bestDist) { bestDist = dist; best = interactable; }
        }
        return best;
    }

    // Shows the reach in the Scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
}
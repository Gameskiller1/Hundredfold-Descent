using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E:
///  - Press on an instant interactable -> interact now.
///  - Press on a hold interactable (HoldTime > 0, e.g. light orbs) -> keep E held for HoldTime.
///  - While carrying a LightOrb: press E -> light the matching unlit torch in range, otherwise drop the orb.
/// </summary>
public class InteractAbility : PlayerAbility
{
    [Header("Interact")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private LayerMask interactableLayer;   // set to "Interactable"
    [SerializeField] private Vector2 offset = Vector2.zero;
    private Interactable shownPrompt;
    [Tooltip("Where a carried orb is dropped, in front of the player.")]
    [SerializeField] private float dropDistance = 0.6f;
    public Interactable CurrentTarget { get; private set; }
    public LightOrb Carried { get; private set; }
    /// <summary>Hold interactable being held right now (null = none).</summary>
    public Interactable Holding { get; private set; }
    /// <summary>0..1 hold progress, for UI rings.</summary>
    public float HoldProgress { get; private set; }

    private readonly List<Collider2D> hits = new List<Collider2D>();
    private ContactFilter2D filter;
    private float holdTimer;

    private void Reset() => cooldown = 0.2f;

    protected override void Awake()
    {
        base.Awake();
        filter = new ContactFilter2D();
        filter.SetLayerMask(interactableLayer);
        filter.useTriggers = true;
    }

    public Interactable AttachTarget { get; private set; } // torch while carrying

    public bool TryCarry(LightOrb orb)
    {
        if (Carried != null) return false;
        Carried = orb;
        EndHold();
        return true;
    }

    private void Update()
    {
        if (Carried != null)
        {
            CurrentTarget = null;
            AttachTarget = FindAttachTarget();
        }
        else
        {
            CurrentTarget = FindNearest();
            AttachTarget = null;
        }
        UpdatePrompt();
        UpdateHold();
    }

    private Interactable FindAttachTarget()
    {
        Interactable best = null;
        float bestDist = float.MaxValue;
        Vector2 center = (Vector2)transform.position + offset;
        foreach (var t in Nearby<LightableTorch>())
        {
            if (!t.CanInteract(player)) continue;
            if (!t.ShouldShowPrompt(player, this)) continue; // ID must match
            float d = ((Vector2)t.transform.position - center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    private void UpdatePrompt()
    {
        if (shownPrompt != null && shownPrompt != CurrentTarget && shownPrompt != AttachTarget)
            shownPrompt.SetPrompt(false);

        Interactable show = (Interactable)AttachTarget ?? CurrentTarget;
        shownPrompt = show;
        if (show == null) return;
        if (!show.ShouldShowPrompt(player, this)) { show.SetPrompt(false); return; }

        string text = (Holding == show)
            ? $"{show.PromptText} {HoldProgress * 100f:0}%"
            : show.GetPromptText();

        show.SetPrompt(true, text);

        // tint + animate the label with the light color
        var pulse = show.GetLabel() != null ? show.GetLabel().GetComponent<PromptPulse>() : null;
        if (pulse != null)
        {
            Color c = show is LightableTorch && Carried != null ? Carried.LightColor
                    : show is LightOrb orb ? orb.LightColor : Color.white;
            pulse.SetLightColor(c);
        }
    }

    private void UseCarried()
    {
        if (AttachTarget is LightableTorch torch && torch.TryIgnite(Carried))
        {
            var orb = Carried;
            Carried = null;
            orb.Consume();
            return;
        }
        Vector2 at = (Vector2)player.transform.position + new Vector2(player.FacingSign * dropDistance, 0.3f);
        var dropped = Carried;
        Carried = null;
        dropped.Drop(at);
    }


    // =====================  PRESS / HOLD  =====================

    protected override bool CanActivate() => Carried != null || CurrentTarget != null;

    protected override void Activate()
    {
        if (Carried != null) { UseCarried(); return; }

        var target = CurrentTarget;
        if (target.HoldTime > 0f)
        {
            Holding = target;
            holdTimer = 0f;
            GameLog.Write(LogLevel.Info, LogCat.Interact, $"holding E on {target.name} ({target.HoldTime:0.0}s)...");
        }
        else
        {
            target.Interact(player);
        }
    }

    private void UpdateHold()
    {
        if (Holding == null || player == null) return;

        bool pressed = inputAction != null && inputAction.action.IsPressed();
        bool far = Vector2.Distance(player.transform.position, Holding.transform.position) > radius * 1.5f;

        if (!pressed || far)
        {
            GameLog.Write(LogLevel.Info, LogCat.Interact,
                $"hold on {Holding.name} cancelled at {holdTimer:0.00}s / {Holding.HoldTime:0.00}s ({(far ? "moved away" : "released early")})");
            EndHold();
            return;
        }

        holdTimer += Time.deltaTime;
        HoldProgress = Mathf.Clamp01(holdTimer / Holding.HoldTime);
        if (holdTimer >= Holding.HoldTime)
        {
            var target = Holding;
            EndHold();
            target.Interact(player);
        }
    }

    private void EndHold()
    {
        Holding = null;
        holdTimer = 0f;
        HoldProgress = 0f;
    }

    // =====================  FINDING  =====================

    private List<T> Nearby<T>() where T : Interactable
    {
        var result = new List<T>();
        Vector2 center = (Vector2)transform.position + offset;
        Physics2D.OverlapCircle(center, radius, filter, hits);
        foreach (var hit in hits)
        {
            // GetComponentInParent: the collider can be on a child of the object
            var t = hit.GetComponentInParent<T>();
            if (t != null && !result.Contains(t)) result.Add(t);
        }
        return result;
    }

    private Interactable FindNearest()
    {
        if (player == null) return null;
        Vector2 center = (Vector2)transform.position + offset;

        Interactable best = null;
        float bestDist = float.MaxValue;
        foreach (var t in Nearby<Interactable>())
        {
            if (!t.CanInteract(player)) continue;
            float d = ((Vector2)t.transform.position - center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector2)transform.position + offset, radius);
    }
}
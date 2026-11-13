using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A health bar for any Health: player HUD, boss bar. Set the Fill Image's Image Type to "Filled".
/// Optional "trail" image (the pale bar that catches up) and name / value labels.
/// Player HUD: leave Target empty and tick "Find Player".  Boss: BossEncounter calls Bind().
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health target;
    [Tooltip("Binds to the player's Health automatically.")]
    [SerializeField] private bool findPlayer = false;

    [Header("Images (Image Type = Filled)")]
    [SerializeField] private Image fill;
    [Tooltip("Optional: slower bar behind the fill that shows damage just taken.")]
    [SerializeField] private Image trail;
    [SerializeField, Min(0f)] private float trailSpeed = 0.6f;   // fraction per second

    [Header("Labels (optional)")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text valueLabel;                // "42 / 100"

    private float trailValue = 1f;

    private void OnEnable() => Subscribe(target);

    private void OnDisable() => Unsubscribe(target);

    private void Update()
    {
        if (findPlayer && target == null && PlayerController.Instance != null)
            Bind(PlayerController.Instance.GetComponent<Health>());

        if (target == null || trail == null) return;
        float now = target.Normalized;
        trailValue = trailValue > now ? Mathf.MoveTowards(trailValue, now, trailSpeed * Time.unscaledDeltaTime) : now;
        trail.fillAmount = trailValue;
    }

    /// <summary>Show a different Health (and optionally a name).</summary>
    public void Bind(Health health, string label = null)
    {
        Unsubscribe(target);
        target = health;
        if (nameLabel != null && label != null) nameLabel.text = label;

        if (target != null)
        {
            trailValue = target.Normalized;
            if (isActiveAndEnabled) Subscribe(target);
            Refresh(target.Current, target.Max);
        }
    }

    private void Subscribe(Health h)
    {
        if (h == null) return;
        h.Changed -= Refresh;       // never double-subscribe
        h.Changed += Refresh;
        Refresh(h.Current, h.Max);
    }

    private void Unsubscribe(Health h)
    {
        if (h != null) h.Changed -= Refresh;
    }

    private void Refresh(float current, float max)
    {
        if (fill != null) fill.fillAmount = max > 0f ? current / max : 0f;
        if (valueLabel != null) valueLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}

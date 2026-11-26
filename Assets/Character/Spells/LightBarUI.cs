using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Light (mana) bar for the HUD. Image Type = Filled. Optional pale "trail" bar and a "42 / 100" label.
/// Finds the player's PlayerLight by itself, even after scene changes.
/// </summary>
public class LightBarUI : MonoBehaviour
{
    [SerializeField] private Image fill;
    [Tooltip("Optional slower bar behind the fill that shows what was just spent.")]
    [SerializeField] private Image trail;
    [SerializeField, Min(0f)] private float trailSpeed = 0.6f;
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("Fill colour while casting would cost health (Light too low for your spells). Optional.")]
    [SerializeField] private Color lowColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.15f;

    private PlayerLight light;
    private Color normalColor = Color.white;
    private float trailValue = 1f;

    private void Awake() { if (fill != null) normalColor = fill.color; }
    private void OnDisable() => Unbind();

    private void Update()
    {
        var p = PlayerController.Instance;
        PlayerLight found = p != null ? p.GetComponent<PlayerLight>() : null;
        if (found != light) Bind(found);
        if (light == null || trail == null) return;

        float now = light.Normalized;
        trailValue = trailValue > now ? Mathf.MoveTowards(trailValue, now, trailSpeed * Time.unscaledDeltaTime) : now;
        trail.fillAmount = trailValue;
    }

    private void Bind(PlayerLight l)
    {
        Unbind();
        light = l;
        if (light == null) return;
        light.Changed += Refresh;
        trailValue = light.Normalized;
        Refresh(light.Current, light.Max);
    }

    private void Unbind() { if (light != null) light.Changed -= Refresh; light = null; }

    private void Refresh(float current, float max)
    {
        if (fill != null)
        {
            fill.fillAmount = max > 0f ? current / max : 0f;
            fill.color = fill.fillAmount <= lowThreshold ? lowColor : normalColor;
        }
        if (valueLabel != null) valueLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}
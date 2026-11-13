using UnityEngine;

/// <summary>
/// Hit feedback: tints the sprite(s) when damaged and (optionally) flickers during the short
/// post-hit invulnerability. Works on the player, enemies and bosses.
/// </summary>
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    [Tooltip("Empty = every SpriteRenderer in the children.")]
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField, Min(0f)] private float flashDuration = 0.1f;

    [Header("Invulnerability flicker")]
    [SerializeField] private bool flickerWhileInvulnerable = false;
    [SerializeField, Min(0.01f)] private float flickerInterval = 0.08f;
    [SerializeField, Range(0f, 1f)] private float flickerAlpha = 0.35f;

    private Health health;
    private Color[] baseColors;
    private float flashUntil;
    private bool modified;

    private void Awake()
    {
        health = GetComponentInParent<Health>();
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) baseColors[i] = renderers[i].color;
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
        Restore();
    }

    private void OnDamaged(DamageInfo _) => flashUntil = Time.time + flashDuration;

    private void Update()
    {
        bool flashing = Time.time < flashUntil;
        bool flicker = flickerWhileInvulnerable && health != null && health.IsAlive && health.RecentlyHit;

        if (!flashing && !flicker)
        {
            if (modified) Restore();
            return;
        }

        modified = true;
        bool dim = Mathf.FloorToInt(Time.time / flickerInterval) % 2 == 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = baseColors[i];
            if (flashing) c = new Color(flashColor.r, flashColor.g, flashColor.b, c.a);
            else if (dim) c.a *= flickerAlpha;
            renderers[i].color = c;
        }
    }

    private void Restore()
    {
        modified = false;
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = baseColors[i];
    }
}

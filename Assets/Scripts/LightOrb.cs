using System.Collections.Generic;
using UnityEngine;

public static class LightIds
{
    public static bool Matches(string required, string carried) =>
        string.IsNullOrWhiteSpace(required) ||
        string.Equals(required.Trim(), (carried ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase);

    public static bool ColorsMatch(Color a, Color b, float tolerance = 0.15f) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < tolerance * 3f;
}

public class LightOrb : Interactable
{
    public enum State { Idle, Carried, Dropped, Consumed }

    [Header("Light")]
    [Tooltip("ID must match OR color must match (see Torch settings).")]
    [SerializeField] private string lightId = "red";
    [SerializeField] private Color color = new Color(1f, 0.4f, 0.3f);

    [Header("Tint targets")]
    [Tooltip("Drag your glow + spread objects here. They all get tinted with Color.")]
    [SerializeField] private List<GameObject> tintTargets = new List<GameObject>();
    [Tooltip("ON = also tint every other child automatically. Keep OFF if your Prompt label gets tinted.")]
    [SerializeField] private bool autoTintAllChildren = false;

    [Header("Pick up")]
    [SerializeField, Min(0f)] private float pickupHoldTime = 0.8f;

    [Header("Motion")]
    [SerializeField] private float idleRadius = 0.3f;
    [SerializeField] private float idleSpeed = 1.5f;
    [SerializeField] private float driftRange = 0.2f;
    [SerializeField] private float driftSpeed = 0.5f;
    [SerializeField] private float carriedRadius = 0.9f;
    [SerializeField] private float carriedSpeed = 3f;
    [SerializeField] private float droppedRadius = 0.5f;
    [SerializeField] private float droppedSpeed = 2f;
    [SerializeField, Range(0.1f, 1f)] private float orbitSquash = 0.5f;
    [SerializeField] private float bobAmount = 0.06f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField, Min(1f)] private float followSharpness = 10f;

    public override float HoldTime => pickupHoldTime;
    public string LightId => lightId;
    public Color LightColor => color;
    public State CurrentState { get; private set; } = State.Idle;

    private Vector2 anchor;
    private Vector2 center;
    private float angle;
    private float seed;
    private PlayerController owner;
    private Transform glowPulse; // first tint target, for scale pulse

    private void Awake()
    {
        anchor = transform.position;
        center = anchor;
        seed = Random.value * 100f;
        ApplyColor();
        if (tintTargets.Count > 0 && tintTargets[0] != null)
            glowPulse = tintTargets[0].transform;
        GameLog.Write(LogLevel.Info, LogCat.Interact, $"{name} spawned ({lightId} light) at {Dev.V(anchor)}");
    }

    // Call this if you change color at runtime
    public void ApplyColor()
    {
        HashSet<GameObject> done = new HashSet<GameObject>();
        foreach (var t in tintTargets)
        {
            if (t == null || done.Contains(t)) continue;
            done.Add(t);
            TintObject(t, color);
        }
        if (autoTintAllChildren)
        {
            foreach (Transform child in transform)
            {
                if (child.CompareTag("PromptLabel")) continue; // skip TMP
                if (done.Contains(child.gameObject)) continue;
                TintObject(child.gameObject, color);
            }
        }
    }

    public static void TintObject(GameObject obj, Color c)
    {
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = c;
        // URP Light2D without needing the URP using
        foreach (var comp in obj.GetComponentsInChildren<Component>(true))
        {
            if (comp.GetType().Name != "Light2D") continue;
            var prop = comp.GetType().GetProperty("color");
            if (prop != null && prop.CanWrite) prop.SetValue(comp, c);
        }
        foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = c;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float radius, speed;
        switch (CurrentState)
        {
            case State.Carried:
                if (owner == null) return;
                {
                    Vector2 target = (Vector2)owner.transform.position + new Vector2(0f, 0.9f);
                    center = Vector2.Lerp(center, target, 1f - Mathf.Exp(-followSharpness * dt));
                }
                radius = carriedRadius; speed = carriedSpeed; break;
            case State.Dropped:
                center = anchor; radius = droppedRadius; speed = droppedSpeed; break;
            case State.Idle:
                float t = Time.time * driftSpeed + seed;
                Vector2 drift = new Vector2(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * driftRange;
                center = anchor + drift; radius = idleRadius; speed = idleSpeed; break;
            default: return;
        }
        angle += speed * dt;
        Vector2 orbit = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * orbitSquash);
        float bob = Mathf.Sin(Time.time * bobSpeed + seed) * bobAmount;
        transform.position = center + orbit + new Vector2(0f, bob);
        if (glowPulse != null)
        {
            float front = (Mathf.Sin(angle) + 1f) * 0.5f;
            glowPulse.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.2f, front);
        }
    }

    public override bool ShouldShowPrompt(PlayerController player, InteractAbility ability) =>
    (CurrentState == State.Idle || CurrentState == State.Dropped) &&
    (ability == null || ability.Carried == null);

    public override string GetPromptText() =>
        $"Hold E {HoldTime:0.#}s: Pick up";

    public override bool CanInteract(PlayerController player) =>
        CurrentState == State.Idle || CurrentState == State.Dropped;

    public override void Interact(PlayerController player) => PickUp(player);

    public void PickUp(PlayerController player)
    {
        var ability = player.GetComponentInChildren<InteractAbility>();
        if (ability == null) { GameLog.Write(LogLevel.Critical, LogCat.Interact, $"{name}: pickup failed, no InteractAbility"); return; }
        if (!ability.TryCarry(this)) return;
        owner = player;
        CurrentState = State.Carried;
        GameLog.Write(LogLevel.Success, LogCat.Interact, $"{name} picked up ({lightId} light). E = drop, or E at matching torch = light it");
    }

    public void Drop(Vector2 at)
    {
        owner = null; anchor = at; CurrentState = State.Dropped;
        GameLog.Write(LogLevel.Info, LogCat.Interact, $"{name} dropped at {Dev.V(at)}");
    }

    public void Consume()
    {
        owner = null; CurrentState = State.Consumed;
        GameLog.Write(LogLevel.Success, LogCat.Interact, $"{name} ({lightId} light) was used up");
        Destroy(gameObject);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =====================================================================
//  SPELL: runtime behaviour, spawned by SpellCaster from a SpellData.
//  Use checks: CanAct -> Cooldown -> GlobalRule -> IsUsable() -> Light -> CanCast()
// =====================================================================
public abstract class Spell : MonoBehaviour
{
    /// <summary>Rule for ALL spells (no-magic rooms...). null = everything allowed.</summary>
    public static Func<Spell, bool> GlobalRule;

    public SpellData Data { get; private set; }
    public SpellCaster Caster { get; private set; }
    protected PlayerController player;
    protected PlayerLight Light => Caster != null ? Caster.Light : null;

    public event Action<Spell> Casted;

    private float readyTime;
    private readonly List<GameObject> spawned = new List<GameObject>();

    public float LightCost => Data != null ? Data.LightCost : 0f;
    public float CooldownRemaining => Mathf.Max(0f, readyTime - Time.time);
    public bool IsOnCooldown => CooldownRemaining > 0f;
    /// <summary>0 = ready, 1 = just cast. For UI cooldown fill.</summary>
    public float CooldownNormalized =>
        Data != null && Data.Cooldown > 0f ? CooldownRemaining / Data.Cooldown : 0f;

    /// <summary>Called by SpellCaster right after spawning.</summary>
    public void Init(SpellCaster caster, SpellData data)
    {
        Caster = caster;
        Data = data;
        player = caster.Player;
        OnInit();
    }

    protected virtual void OnInit() { }

    // ---------- Checks ----------

    /// <summary>THE general override: "is this spell allowed right now?" (UI can grey out).</summary>
    public virtual bool IsUsable() => true;

    /// <summary>Internal state checks for spell types (e.g. not already aiming).</summary>
    protected virtual bool CanCast() => true;

    public bool CanAffordCost => Light == null || Light.CanAfford(LightCost);

    /// <summary>Everything except cooldown/CanAct - for UI.</summary>
    public bool IsAllowed => (GlobalRule == null || GlobalRule(this)) && IsUsable() && CanAffordCost;

    /// <summary>Called by SpellCaster when the slot key is pressed.</summary>
    public bool TryCast()
    {
        if (player == null || !player.CanAct || IsOnCooldown) return false;
        if (!IsAllowed || !CanCast()) return false;
        Begin();
        return true;
    }

    /// <summary>Instant: casts. Targeted: starts aiming.</summary>
    protected abstract void Begin();

    /// <summary>Stop anything in progress (aiming etc.). Called on unequip.</summary>
    public virtual void ForceCancel() { }

    // ---------- Helpers ----------

    protected bool PayCost() => Light == null || Light.TrySpend(LightCost);

    /// <summary>Call when the spell actually went off: starts cooldown + fires events.</summary>
    protected void NotifyCast()
    {
        readyTime = Time.time + (Data != null ? Data.Cooldown : 0f);
        Casted?.Invoke(this);
    }

    /// <summary>Spawns once, reuses, survives scene loads. Cleaned up when unequipped.</summary>
    protected T GetOrSpawn<T>(T prefab, ref T instance) where T : Component
    {
        if (instance == null && prefab != null)
        {
            instance = Instantiate(prefab);
            DontDestroyOnLoad(instance.gameObject);
            spawned.Add(instance.gameObject);
        }
        return instance;
    }

    protected GameObject GetOrSpawn(GameObject prefab, ref GameObject instance)
    {
        if (instance == null && prefab != null)
        {
            instance = Instantiate(prefab);
            DontDestroyOnLoad(instance);
            instance.SetActive(false);
            spawned.Add(instance);
        }
        return instance;
    }

    protected virtual void OnDestroy()
    {
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
    }
}

// =====================================================================
//  INSTANT SPELL: key -> pay light -> Cast().
// =====================================================================
public abstract class InstantSpell : Spell
{
    protected abstract void Cast();

    protected sealed override void Begin()
    {
        if (!PayCost()) return;
        Cast();
        NotifyCast();
    }
}

// =====================================================================
//  TARGETED SPELL: key -> (pause) -> aim -> LMB confirm (pays light) / Q cancel (free)
//  Inputs come from SpellCaster, so spell prefabs don't need any.
// =====================================================================
public abstract class TargetedSpell : Spell
{
    [Header("Targeting")]
    [SerializeField] private bool pauseGame = true;
    [Tooltip("Max distance from the player. 0 = unlimited.")]
    [SerializeField] private float maxRange = 8f;

    [Header("Ground Snap")]
    [SerializeField] private bool snapToGround = true;
    [Tooltip("Same layer(s) as PlayerController's ground check.")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float snapDistance = 15f;
    [Tooltip("Invalid if nothing is below the cursor (pits, empty air).")]
    [SerializeField] private bool requireGround = true;

    [Header("Pixel Snap")]
    [Tooltip("Match your sprites' Pixels Per Unit. 0 = off.")]
    [SerializeField] private float pixelsPerUnit = 16f;

    [Header("Reticle (optional)")]
    [SerializeField] private GameObject reticlePrefab;
    [SerializeField] private Color validColor = Color.white;
    [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    public static TargetedSpell Active { get; private set; }

    public bool IsTargeting { get; private set; }
    public bool IsTargetValid { get; private set; }
    public Vector2 TargetPoint { get; private set; }

    private bool waitingForRelease;
    private float previousTimeScale = 1f;
    private Camera cam;
    private GameObject reticleInstance;
    private SpriteRenderer reticleRenderer;

    protected abstract void Cast(Vector2 point);

    /// <summary>Extra per-point checks (line of sight, enemy under cursor...).</summary>
    protected virtual bool IsValidTarget(Vector2 point) => true;

    protected override bool CanCast() => !IsTargeting && !waitingForRelease && Active == null;

    // ---------- Flow ----------

    protected sealed override void Begin()
    {
        IsTargeting = true;
        Active = this;
        player.LockActions();

        if (pauseGame)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        SetTargetInput(true);
        if (GetOrSpawn(reticlePrefab, ref reticleInstance) != null) reticleInstance.SetActive(true);
        Caster.SetDim(true);
        UpdateTarget();
    }

    protected virtual void Update()
    {
        // Keep actions locked until LMB is released so the confirm click can't also Attack
        if (waitingForRelease)
        {
            var c = Caster.ConfirmAction;
            if (c == null || !c.action.IsPressed())
            {
                waitingForRelease = false;
                player.UnlockActions();
            }
            return;
        }

        if (IsTargeting) UpdateTarget();
    }

    private void OnConfirm(InputAction.CallbackContext _)
    {
        if (!IsTargeting) return;

        Vector2 point = ComputeTargetPoint(out bool valid);
        if (!valid) return;                       // bad spot: keep aiming

        if (!PayCost()) { EndTargeting(unlockNow: true); return; }

        EndTargeting(unlockNow: false);
        waitingForRelease = true;

        Cast(point);
        NotifyCast();                             // cooldown starts on the actual cast
    }

    private void OnCancel(InputAction.CallbackContext _)
    {
        if (IsTargeting) EndTargeting(unlockNow: true);   // no cost, no cooldown
    }

    public override void ForceCancel()
    {
        if (IsTargeting) EndTargeting(unlockNow: true);
        if (waitingForRelease) { waitingForRelease = false; player.UnlockActions(); }
    }

    private void EndTargeting(bool unlockNow)
    {
        IsTargeting = false;
        if (Active == this) Active = null;
        SetTargetInput(false);

        if (pauseGame) Time.timeScale = previousTimeScale;
        if (reticleInstance != null) reticleInstance.SetActive(false);
        if (Caster != null) Caster.SetDim(false);

        if (unlockNow) player.UnlockActions();
    }

    // ---------- Target point ----------

    private void UpdateTarget()
    {
        TargetPoint = ComputeTargetPoint(out bool valid);
        IsTargetValid = valid;

        if (reticleInstance == null) return;
        reticleInstance.transform.position = TargetPoint;
        if (reticleRenderer == null) reticleRenderer = reticleInstance.GetComponentInChildren<SpriteRenderer>();
        if (reticleRenderer != null) reticleRenderer.color = valid ? validColor : invalidColor;
    }

    private Vector2 ComputeTargetPoint(out bool valid)
    {
        valid = true;
        if (cam == null) cam = Camera.main;

        var p = Caster.PointAction;
        Vector2 screen = p != null ? p.action.ReadValue<Vector2>()
                       : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        Vector2 point = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        if (maxRange > 0f)
        {
            Vector2 origin = player.transform.position;
            point = origin + Vector2.ClampMagnitude(point - origin, maxRange);
        }

        if (snapToGround)
        {
            if (Physics2D.OverlapPoint(point, groundLayer) != null)
            {
                valid = false;                            // cursor inside a wall/floor
            }
            else
            {
                RaycastHit2D hit = Physics2D.Raycast(point, Vector2.down, snapDistance, groundLayer);
                if (hit.collider != null) point = hit.point;
                else if (requireGround) valid = false;    // nothing below
            }
        }

        if (pixelsPerUnit > 0f)
        {
            point.x = Mathf.Round(point.x * pixelsPerUnit) / pixelsPerUnit;
            point.y = Mathf.Round(point.y * pixelsPerUnit) / pixelsPerUnit;
        }

        if (valid && !IsValidTarget(point)) valid = false;
        return point;
    }

    // ---------- Input ----------

    private void SetTargetInput(bool on)
    {
        var c = Caster.ConfirmAction;
        var x = Caster.CancelAction;
        var p = Caster.PointAction;

        if (on)
        {
            if (c != null) { c.action.Enable(); c.action.performed += OnConfirm; }
            if (x != null) { x.action.Enable(); x.action.performed += OnCancel; }
            if (p != null) p.action.Enable();
        }
        else
        {
            if (c != null) c.action.performed -= OnConfirm;
            if (x != null) x.action.performed -= OnCancel;
        }
    }

    // ---------- Safety ----------

    protected virtual void OnDisable() => ForceCancel();   // never leave the game frozen

    protected virtual void OnDrawGizmosSelected()
    {
        if (maxRange <= 0f || player == null) return;
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireSphere(player.transform.position, maxRange);
    }
}

// =====================================================================
//  SPELL EFFECT: the spawned visual. Root stays active,
//  child "visual" is toggled with SetActive. Call Play(position).
// =====================================================================
public abstract class SpellEffect : MonoBehaviour
{
    [Tooltip("Child with SpriteRenderer + Animator. Auto-uses first child if empty.")]
    [SerializeField] protected GameObject visual;
    [Tooltip("How long the visual stays on. <= 0 = Animator clip length.")]
    [SerializeField] protected float duration = -1f;

    public bool IsPlaying { get; private set; }
    private Animator visualAnimator;

    protected virtual void Awake()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0).gameObject;
        if (visual == null) { Debug.LogError($"[{name}] No visual child assigned.", this); return; }
        visualAnimator = visual.GetComponent<Animator>();
        visual.SetActive(false);
    }

    public void Play(Vector2 position)
    {
        if (visual == null) return;
        transform.position = position;
        StopAllCoroutines();

        visual.SetActive(false);    // off -> on restarts the animation from frame 1
        visual.SetActive(true);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        IsPlaying = true;
        yield return PlayRoutine(GetDuration());
        visual.SetActive(false);
        IsPlaying = false;
    }

    /// <summary>Override for timed logic (hits, sounds). Must last 'length' seconds total.</summary>
    protected virtual IEnumerator PlayRoutine(float length)
    {
        yield return new WaitForSeconds(length);
    }

    protected float GetDuration()
    {
        if (duration > 0f) return duration;
        if (visualAnimator != null && visualAnimator.runtimeAnimatorController != null)
        {
            var clips = visualAnimator.runtimeAnimatorController.animationClips;
            if (clips.Length > 0) return clips[0].length;
        }
        return 0.5f;
    }

    protected virtual void OnDisable()
    {
        if (visual != null) visual.SetActive(false);
        IsPlaying = false;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Watches the whole game and writes what happens to GameLog: input, abilities, animations, spells, light,
/// health/damage, every enemy, bosses, scenes, Unity warnings/errors. Also flags suspicious states
/// (stuck locks, frozen time, falling out of the world). Created by DebugConsole; needs no setup.
/// </summary>
[DefaultExecutionOrder(10000)]
public class GameLogger : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Actions that are never logged (continuous / spammy).")]
    [SerializeField] private string[] ignoredInputs = { "Look", "Point", "Navigate", "ScrollWheel", "TrackedDevicePosition", "TrackedDeviceOrientation" };

    [Header("Bug detection")]
    [Tooltip("A lock held longer than this with nothing explaining it = warning.")]
    [SerializeField, Min(1f)] private float stuckSeconds = 6f;
    [SerializeField] private float fallOutY = -100f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;

    // ---------------- enemy tracking ----------------
    private class ET
    {
        public Enemy e; public string tag; public PatrolChaseBrain brain; public float born;
        public bool dormant, stunned, knocked, alerted, blocked, warnedLock;
        public string anim; public int facing, mLock, aLock; public float lockSince = -1f;
        public readonly Dictionary<EnemyAttack, float> running = new Dictionary<EnemyAttack, float>();
    }
    private readonly Dictionary<int, ET> tracks = new Dictionary<int, ET>();
    private readonly List<int> dead = new List<int>();
    private readonly Dictionary<BossEncounter, int> encounters = new Dictionary<BossEncounter, int>();
    private float scanTimer;

    // ---------------- player tracking ----------------
    private PlayerController player; private Health pHealth; private PlayerLight pLight; private SpellCaster pCaster;
    private MeleeAttackAbility pAttack; private PlayerHealth pPH; private DashAbility pDash;
    private bool wasGrounded = true; private float airStart, airMinVy;
    private PlayerAnim lastAnim; private float animSince; private int lastFacing = 1;
    private int mLock, aLock, gLock; private float mLockSince = -1f, aLockSince = -1f; private bool warnedM, warnedA;
    private TargetedSpell lastAiming; private float lightPrev; private bool lightEmpty;
    private float lastTimeScale = 1f, frozenSince = -1f; private bool warnedFrozen, fellOut; private float lastSpike;
    private int attackHits;
    private readonly Dictionary<string, float> throttle = new Dictionary<string, float>();
    private HashSet<string> ignored;

    // =====================  SETUP  =====================

    private void OnEnable()
    {
        ignored = new HashSet<string>(ignoredInputs);
        Application.logMessageReceived += OnUnityLog;
        InputSystem.onActionChange += OnInputChange;
        PlayerAbility.Activated += OnAbilityActivated;
        PlayerAbility.Blocked += OnAbilityBlocked;
        Spell.CastBlocked += OnSpellBlocked;
        Health.Rejected += OnHitRejected;
        PlayerHealth.PlayerDied += OnPlayerDied;
        PlayerHealth.PlayerRespawned += OnPlayerRespawned;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Log(LogCat.System, LogLevel.Info, $"logger started. scene '{SceneManager.GetActiveScene().name}', Unity {Application.unityVersion}, {(Application.isEditor ? "editor" : "build")}");
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnUnityLog;
        InputSystem.onActionChange -= OnInputChange;
        PlayerAbility.Activated -= OnAbilityActivated;
        PlayerAbility.Blocked -= OnAbilityBlocked;
        Spell.CastBlocked -= OnSpellBlocked;
        Health.Rejected -= OnHitRejected;
        PlayerHealth.PlayerDied -= OnPlayerDied;
        PlayerHealth.PlayerRespawned -= OnPlayerRespawned;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        UnhookPlayer();
    }

    private static void Log(LogCat cat, LogLevel lv, string msg) => GameLog.Write(lv, cat, msg);

    /// <summary>Same message at most once per interval (stops per-frame spam).</summary>
    private bool Throttled(string key, float interval)
    {
        if (throttle.TryGetValue(key, out float t) && Time.unscaledTime < t) return true;
        throttle[key] = Time.unscaledTime + interval;
        return false;
    }

    // =====================  UNITY LOG / INPUT / SCENES  =====================

    private void OnUnityLog(string cond, string stack, LogType type)
    {
        string one = cond.Replace("\n", " | ");
        string where = FirstLines(stack, 2);
        switch (type)
        {
            case LogType.Warning: Log(LogCat.System, LogLevel.Warning, "[Unity] " + one); break;
            case LogType.Error:
            case LogType.Assert: Log(LogCat.System, LogLevel.Error, "[Unity] " + one + where); break;
            case LogType.Exception: Log(LogCat.System, LogLevel.Critical, "[EXCEPTION] " + one + where); break;
            default: Log(LogCat.System, LogLevel.Info, "[Unity] " + one); break;
        }
    }

    private static string FirstLines(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var lines = s.Split('\n'); string r = "";
        for (int i = 0; i < Mathf.Min(n, lines.Length); i++) if (lines[i].Length > 0) r += "  @ " + lines[i].Trim();
        return r;
    }

    private void OnInputChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionStarted && change != InputActionChange.ActionPerformed && change != InputActionChange.ActionCanceled) return;
        var a = obj as InputAction;
        if (a == null || ignored.Contains(a.name)) return;
        if (a.actionMap != null && a.actionMap.name == "UI") return;
        string ctl = a.activeControl != null ? a.activeControl.path : "?";

        if (a.type == InputActionType.Button)
        {
            if (change == InputActionChange.ActionPerformed) Log(LogCat.Input, LogLevel.Info, $"Player action: {a.name} pressed ({ctl})");
            else if (change == InputActionChange.ActionCanceled) Log(LogCat.Input, LogLevel.Trace, $"Player action: {a.name} released");
        }
        else
        {
            if (change == InputActionChange.ActionStarted) Log(LogCat.Input, LogLevel.Trace, $"Player action: {a.name} started ({a.ReadValueAsObject()})");
            else if (change == InputActionChange.ActionCanceled) Log(LogCat.Input, LogLevel.Trace, $"Player action: {a.name} stopped");
        }
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m) => Log(LogCat.Scene, LogLevel.Info, $"scene loaded: {s.name} ({m})");
    private void OnSceneUnloaded(Scene s) => Log(LogCat.Scene, LogLevel.Info, $"scene unloaded: {s.name}");
    private void OnActiveSceneChanged(Scene a, Scene b) => Log(LogCat.Scene, LogLevel.Info, $"active scene: {a.name} -> {b.name}");

    // =====================  ABILITIES / SPELLS / HITS  =====================

    private void OnAbilityActivated(PlayerAbility a) => Log(LogCat.Ability, LogLevel.Info, $"Player script: {a.GetType().Name} activated");
    private void OnAbilityBlocked(PlayerAbility a, string why) => Log(LogCat.Ability, LogLevel.Info, $"Player script: {a.GetType().Name} BLOCKED - {why}");
    private void OnSpellBlocked(Spell s, string why) => Log(LogCat.Spell, LogLevel.Info, $"Spell {(s.Data != null ? s.Data.DisplayName : s.name)} BLOCKED - {why}");

    private void OnHitRejected(Health h, DamageInfo i, string why)
    {
        string who = Dev.NameOf(h), src = Dev.NameOf(i.source);
        if (Throttled($"rej|{who}|{src}|{why}", 0.5f)) return;
        Log(LogCat.Combat, LogLevel.Info, $"Hit on {who} REJECTED ({why}): {i.amount:0.#} {i.type} from {src}");
    }

    private void OnPlayerDied() => Log(LogCat.Player, LogLevel.Critical, "PLAYER DIED");
    private void OnPlayerRespawned() => Log(LogCat.Player, LogLevel.Success, "Player respawned");

    // =====================  PLAYER  =====================

    private void HookPlayer(PlayerController p)
    {
        UnhookPlayer();
        player = p;
        if (p == null) return;

        pHealth = p.GetComponent<Health>(); pLight = p.GetComponent<PlayerLight>(); pPH = p.GetComponent<PlayerHealth>();
        pCaster = p.GetComponentInChildren<SpellCaster>(); pAttack = p.GetComponentInChildren<MeleeAttackAbility>(); pDash = p.GetComponentInChildren<DashAbility>();

        if (pHealth != null)
        {
            pHealth.Damaged += OnPlayerDamaged;
            pHealth.Healed += h => Log(LogCat.Player, LogLevel.Info, $"Player healed +{h:0.#} -> HP {pHealth.Current:0.#}/{pHealth.Max:0.#}");
            pHealth.Revived += () => Log(LogCat.Player, LogLevel.Info, $"Player revived, HP {pHealth.Current:0.#}/{pHealth.Max:0.#}");
        }
        if (pLight != null) { lightPrev = pLight.Current; pLight.Changed += OnLightChanged; pLight.Overdrew += OnOverdrew; }
        if (pCaster != null)
        {
            pCaster.SlotChanged += (i, d) => Log(LogCat.Spell, LogLevel.Info, $"Spell slot {i + 1}: {(d != null ? d.DisplayName : "(empty)")}");
            pCaster.SpellCast += s => Log(LogCat.Spell, LogLevel.Info, $"Spell cast: {s.Data.DisplayName} (cost {s.LightCost}, cooldown {s.Data.Cooldown}s)");
        }
        if (pAttack != null)
        {
            pAttack.Started += () => { attackHits = 0; Log(LogCat.Ability, LogLevel.Info, "Player script: attack preparing"); };
            pAttack.HitWindowOpened += () => Log(LogCat.Ability, LogLevel.Info, "Player script: attack ran and confirmed (hit window open)");
            pAttack.Hit += (h, front) => { attackHits++; Log(LogCat.Ability, LogLevel.Info, $"Player script: attack hit {Dev.NameOf(h)} ({(front ? "front box" : "around circle")})"); };
            pAttack.Finished += () => Log(LogCat.Ability, LogLevel.Info, $"Player script: attack finished ({attackHits} hits)");
        }

        lastAnim = p.Anim != null ? p.Anim.Current : PlayerAnim.Idle; animSince = Time.time; lastFacing = p.FacingSign; wasGrounded = p.IsGrounded;
        Log(LogCat.Player, LogLevel.Info, $"Player tracked at {Dev.V(p.transform.position)}  HP {(pHealth != null ? pHealth.Current : 0):0.#}  Light {(pLight != null ? pLight.Current : 0):0.#}");
        if (pCaster != null)
            for (int i = 0; i < pCaster.SlotCount; i++)
                if (pCaster.GetData(i) != null) Log(LogCat.Spell, LogLevel.Info, $"Spell slot {i + 1}: {pCaster.GetData(i).DisplayName}");
    }

    private void UnhookPlayer()
    {
        // The player's own events die with it; only keep nothing referenced.
        player = null; pHealth = null; pLight = null; pCaster = null; pAttack = null; pPH = null; pDash = null;
    }

    private void OnPlayerDamaged(DamageInfo i)
    {
        Log(LogCat.Player, LogLevel.Warning, $"Player took {i.amount:0.#} {i.type} from {Dev.NameOf(i.source)} -> HP {pHealth.Current:0.#}/{pHealth.Max:0.#}  knockback {Dev.V(i.knockback)}");
    }

    private void OnLightChanged(float cur, float max)
    {
        float d = cur - lightPrev; lightPrev = cur;
        if (d <= -0.01f) Log(LogCat.Light, LogLevel.Info, $"Light {d:0.#} -> {cur:0.#}/{max:0.#}");
        else if (d >= 1f) Log(LogCat.Light, LogLevel.Info, $"Light +{d:0.#} -> {cur:0.#}/{max:0.#}");
        if (cur <= 0f && !lightEmpty) { lightEmpty = true; Log(LogCat.Light, LogLevel.Warning, "Light is EMPTY (casting now costs health)"); }
        else if (cur > 0f) lightEmpty = false;
        if (cur >= max && d > 0f && d < 1f) Log(LogCat.Light, LogLevel.Trace, "Light regenerated to full");
    }

    private void OnOverdrew(float missing, float damage) => Log(LogCat.Light, LogLevel.Warning, $"OVERDRAW: {missing:0.#} Light missing -> paid {damage:0.#} HP");

    private void PollPlayer()
    {
        var p = player;

        // ground
        bool g = p.IsGrounded;
        float vy = p.Rb.linearVelocity.y;
        if (!g) airMinVy = Mathf.Min(airMinVy, vy);
        if (g != wasGrounded)
        {
            if (!g) { airStart = Time.time; airMinVy = vy; Log(LogCat.Player, LogLevel.Trace, $"left ground at {Dev.V(p.transform.position)}, vel {Dev.V(p.Rb.linearVelocity)}"); }
            else Log(LogCat.Player, LogLevel.Trace, $"landed at {Dev.V(p.transform.position)} after {Time.time - airStart:0.00}s in air (fastest fall {airMinVy:0.#})");
            wasGrounded = g;
        }

        if (p.FacingSign != lastFacing) { lastFacing = p.FacingSign; Log(LogCat.Player, LogLevel.Trace, "facing " + (lastFacing > 0 ? "right" : "left")); }

        // animation
        if (p.Anim != null && p.Anim.Current != lastAnim)
        {
            bool loco = lastAnim <= PlayerAnim.Land;
            float dur = Time.time - animSince;
            Log(LogCat.Anim, loco ? LogLevel.Trace : LogLevel.Info, $"Player animation: {lastAnim} finished ({dur:0.00}s)");
            lastAnim = p.Anim.Current; animSince = Time.time;
            Log(LogCat.Anim, lastAnim <= PlayerAnim.Land ? LogLevel.Trace : LogLevel.Info, $"Player animation: {lastAnim} started");
        }

        // locks (private counters read by reflection)
        int m = Dev.Int(p, "movementLocks"), a = Dev.Int(p, "actionLocks"), gr = Dev.Int(p, "gravityLocks");
        if (m != mLock || a != aLock || gr != gLock)
        {
            Log(LogCat.Player, LogLevel.Trace, $"locks changed: movement {mLock}->{m}, actions {aLock}->{a}, gravity {gLock}->{gr}");
            mLock = m; aLock = a; gLock = gr;
        }
        bool dead = pHealth != null && !pHealth.IsAlive;
        bool aimingOrBusy = DebugConsole.IsOpen || dead || TargetedSpell.Active != null || (pAttack != null && pAttack.IsAttacking);
        CheckStuck(a > 0 && !aimingOrBusy, ref aLockSince, ref warnedA, $"Player ACTIONS locked for >{stuckSeconds:0}s with nothing explaining it (count {a}) - a lock was probably never released");
        CheckStuck(m > 0 && !(aimingOrBusy || (pDash != null && pDash.IsDashing)), ref mLockSince, ref warnedM, $"Player MOVEMENT locked for >{stuckSeconds:0}s with nothing explaining it (count {m})");

        // aiming
        var aiming = TargetedSpell.Active;
        if (aiming != lastAiming)
        {
            if (aiming != null) Log(LogCat.Spell, LogLevel.Info, $"Aiming {aiming.Data.DisplayName} (game paused: {Time.timeScale == 0f})");
            else if (lastAiming != null) Log(LogCat.Spell, LogLevel.Info, "Aiming ended (cast or cancelled)");
            lastAiming = aiming;
        }

        // sanity
        Vector3 pos = p.transform.position;
        bool bad = float.IsNaN(pos.x) || float.IsNaN(pos.y) || pos.y < fallOutY;
        if (bad && !fellOut) { fellOut = true; Log(LogCat.Player, LogLevel.Critical, $"Player is OUT OF THE WORLD at {Dev.V(pos)}"); }
        else if (!bad) fellOut = false;
        float speed = p.Rb.linearVelocity.magnitude;
        if (speed > 40f && Time.unscaledTime > lastSpike + 2f) { lastSpike = Time.unscaledTime; Log(LogCat.Player, LogLevel.Warning, $"Player speed spike {speed:0.#} (vel {Dev.V(p.Rb.linearVelocity)})"); }
    }

    private void CheckStuck(bool suspicious, ref float since, ref bool warned, string message)
    {
        if (!suspicious) { since = -1f; warned = false; return; }
        if (since < 0f) since = Time.unscaledTime;
        if (!warned && Time.unscaledTime - since > stuckSeconds) { warned = true; Log(LogCat.Player, LogLevel.Warning, message); }
    }

    private void PollGlobal()
    {
        if (!Mathf.Approximately(Time.timeScale, lastTimeScale))
        {
            Log(LogCat.System, LogLevel.Info, $"timeScale {lastTimeScale:0.##} -> {Time.timeScale:0.##}");
            lastTimeScale = Time.timeScale;
        }
        bool frozen = Time.timeScale == 0f && TargetedSpell.Active == null;
        if (!frozen) { frozenSince = -1f; warnedFrozen = false; }
        else
        {
            if (frozenSince < 0f) frozenSince = Time.unscaledTime;
            if (!warnedFrozen && Time.unscaledTime - frozenSince > 3f) { warnedFrozen = true; Log(LogCat.System, LogLevel.Warning, "timeScale has been 0 for 3s+ but nothing is aiming - game frozen by a bug? (use: timescale 1)"); }
        }
    }

    // =====================  ENEMIES / BOSSES  =====================

    private void Scan()
    {
        foreach (var e in Dev.Enemies()) if (!tracks.ContainsKey(e.GetInstanceID())) AddTrack(e);

        foreach (var b in FindObjectsByType<BossEncounter>(FindObjectsSortMode.None))
        {
            int state = (b.HasStarted ? 1 : 0) + (b.IsDefeated ? 2 : 0);
            if (!encounters.TryGetValue(b, out int old)) { encounters[b] = state; Log(LogCat.Boss, LogLevel.Info, $"Boss arena '{b.name}' tracked"); continue; }
            if (state == old) continue;
            encounters[b] = state;
            if ((state & 2) != 0) Log(LogCat.Boss, LogLevel.Success, $"Boss encounter '{b.name}' DEFEATED");
            else if ((state & 1) != 0) Log(LogCat.Boss, LogLevel.Warning, $"Boss encounter '{b.name}' STARTED");
            else Log(LogCat.Boss, LogLevel.Warning, $"Boss encounter '{b.name}' RESET");
        }
    }

    private void AddTrack(Enemy e)
    {
        var t = new ET { e = e, tag = Dev.Tag(e), brain = e.Brain as PatrolChaseBrain, born = Time.time, dormant = e.IsDormant, facing = e.FacingSign, anim = e.Anim != null ? e.Anim.Current : null };
        tracks[e.GetInstanceID()] = t;

        var names = new List<string>(); foreach (var a in e.Attacks) if (a != null) names.Add(a.name + ":" + a.GetType().Name);
        Log(LogCat.Enemy, LogLevel.Info, $"{t.tag} tracked at {Dev.V(e.transform.position)}  HP {e.Health.Current:0.#}/{e.Health.Max:0.#}  brain {(e.Brain != null ? e.Brain.GetType().Name : "NONE")}  motor {(e.Motor != null ? e.Motor.GetType().Name : "NONE")}  attacks [{string.Join(", ", names)}]{(e.IsDormant ? "  DORMANT" : "")}");
        if (e.Brain == null) Log(LogCat.Enemy, LogLevel.Warning, $"{t.tag} has no brain - it will never act");
        if (e.Motor == null) Log(LogCat.Enemy, LogLevel.Warning, $"{t.tag} has no motor - it can't move");
        if (e.Anim == null) Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} has no EnemyAnimator");

        e.Health.Damaged += i => Log(LogCat.Combat, LogLevel.Info, $"{t.tag} took {i.amount:0.#} {i.type} from {Dev.NameOf(i.source)} -> HP {e.Health.Current:0.#}/{e.Health.Max:0.#}  knockback {Dev.V(i.knockback)}");
        e.Health.Healed += h => Log(LogCat.Combat, LogLevel.Info, $"{t.tag} healed +{h:0.#}");
        e.Health.Died += i => Log(LogCat.Enemy, LogLevel.Success, $"{t.tag} DIED (killed by {Dev.NameOf(i.source)}, lived {Time.time - t.born:0.0}s)");
        e.Stunned += _ => Log(LogCat.Enemy, LogLevel.Info, $"{t.tag} stunned");

        var boss = e.GetComponent<BossPhases>();
        if (boss != null) boss.PhaseChanged += (idx, ph) => Log(LogCat.Boss, LogLevel.Warning, $"{t.tag} entered phase {idx + 1}: '{ph.name}' (HP {e.Health.Normalized * 100f:0}%)");
    }

    private void PollEnemy(int id, ET t)
    {
        var e = t.e;
        if (e == null) { dead.Add(id); Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} removed from the scene"); return; }

        if (e.IsDormant != t.dormant) { t.dormant = e.IsDormant; Log(LogCat.Enemy, LogLevel.Info, $"{t.tag} {(t.dormant ? "went DORMANT" : "WOKE UP")}"); }
        if (e.IsStunned != t.stunned) { t.stunned = e.IsStunned; Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} {(t.stunned ? "stun started" : "stun ended")}"); }
        if (e.IsKnockedBack != t.knocked) { t.knocked = e.IsKnockedBack; Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} {(t.knocked ? "knockback started" : "knockback ended")}"); }
        if (t.brain != null && t.brain.IsAlerted != t.alerted)
        {
            t.alerted = t.brain.IsAlerted;
            Log(LogCat.Enemy, LogLevel.Info, t.alerted ? $"{t.tag} NOTICED the player (distance {e.DistanceToTarget:0.0})" : $"{t.tag} lost track of the player");
        }

        foreach (var a in e.Attacks)
        {
            if (a == null) continue;
            bool known = t.running.ContainsKey(a);
            if (a.IsRunning && !known) { t.running[a] = Time.time; Log(LogCat.Enemy, LogLevel.Info, $"{t.tag} attack '{a.name}' ({a.GetType().Name}) STARTED  target distance {e.DistanceToTarget:0.0}  HP {e.Health.Current:0.#}"); }
            else if (!a.IsRunning && known) { Log(LogCat.Enemy, LogLevel.Info, $"{t.tag} attack '{a.name}' ended after {Time.time - t.running[a]:0.00}s"); t.running.Remove(a); }
            else if (known && Time.time - t.running[a] > 15f && !Throttled("atk|" + id + a.name, 15f)) Log(LogCat.Enemy, LogLevel.Warning, $"{t.tag} attack '{a.name}' has been running {Time.time - t.running[a]:0}s - stuck?");
        }

        if (e.Anim != null && e.Anim.Current != t.anim) { Log(LogCat.Anim, LogLevel.Trace, $"{t.tag} animation {t.anim ?? "-"} -> {e.Anim.Current}"); t.anim = e.Anim.Current; }
        if (e.FacingSign != t.facing) { t.facing = e.FacingSign; Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} turned {(t.facing > 0 ? "right" : "left")}"); }
        bool blocked = e.Motor != null && e.Motor.IsBlocked;
        if (blocked != t.blocked) { t.blocked = blocked; if (blocked) Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} blocked by wall/ledge"); }

        int m = Dev.Int(e, "movementLocks"), ai = Dev.Int(e, "aiLocks");
        if (m != t.mLock || ai != t.aLock) { Log(LogCat.Enemy, LogLevel.Trace, $"{t.tag} locks: movement {t.mLock}->{m}, ai {t.aLock}->{ai}"); t.mLock = m; t.aLock = ai; }
        bool explained = e.IsDead || e.IsAttacking || e.IsDormant || Dev.AiFrozen || e.GetComponent<BossPhases>() != null;
        CheckStuckEnemy(t, m > 0 && !explained);
    }

    private void CheckStuckEnemy(ET t, bool suspicious)
    {
        if (!suspicious) { t.lockSince = -1f; t.warnedLock = false; return; }
        if (t.lockSince < 0f) t.lockSince = Time.unscaledTime;
        if (!t.warnedLock && Time.unscaledTime - t.lockSince > stuckSeconds) { t.warnedLock = true; Log(LogCat.Enemy, LogLevel.Warning, $"{t.tag} MOVEMENT locked for >{stuckSeconds:0}s while not attacking - stuck lock?"); }
    }

    // =====================  LOOP  =====================

    private void Update()
    {
        var p = PlayerController.Instance;
        if (p != player) HookPlayer(p);
        if (player != null) PollPlayer();
        PollGlobal();

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f) { scanTimer = scanInterval; Scan(); }

        dead.Clear();
        foreach (var kv in tracks) PollEnemy(kv.Key, kv.Value);
        foreach (var id in dead) tracks.Remove(id);
    }
}
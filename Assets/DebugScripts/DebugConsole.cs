using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// In-game console (IMGUI, zero UI setup). Opens with F1 / ` or your own input action.
/// Auto-creates itself when the game starts; put one in the scene only to fill the spawn / spell lists.
/// While open, the player's actions and movement are locked.
/// </summary>
[DisallowMultipleComponent]
public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Header("Toggle")]
    [Tooltip("Optional input action (e.g. a 'Console' button). Empty = F1 or the key left of 1.")]
    [SerializeField] private InputActionReference toggleAction;
    [SerializeField] private bool startOpen = false;
    [Tooltip("Off = removed from release builds (editor + Development Builds only).")]
    [SerializeField] private bool enableInRelease = false;

    [Header("Look")]
    [SerializeField, Range(0.25f, 0.95f)] private float heightFraction = 0.55f;
    [SerializeField, Range(0.5f, 3f)] private float uiScale = 1f;
    [SerializeField, Range(0.3f, 1f)] private float backgroundAlpha = 0.9f;

    [Header("Saving ('savelog' command)")]
    [Tooltip("Editor: Assets/<folder>. Build: persistentDataPath/<folder>.")]
    [SerializeField] private string logFolder = "ConsoleLog";

    [Header("Command content")]
    [Tooltip("Prefabs 'spawn' can create (by name). Scene enemies can always be cloned too.")]
    [SerializeField] private List<GameObject> spawnables = new List<GameObject>();
    [Tooltip("Extra spells 'equip' knows. Spells already loaded in memory are found automatically.")]
    [SerializeField] private List<SpellData> spells = new List<SpellData>();

    public bool ShowStats { get; set; }
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;
    public bool[] CatOn { get; } = new bool[Enum.GetValues(typeof(LogCat)).Length];
    public IReadOnlyList<GameObject> Spawnables => spawnables;
    public IReadOnlyList<SpellData> Spells => spells;
    public void MarkDirty() => seenVersion = -1;

    private readonly List<LogEntry> visible = new List<LogEntry>();
    private readonly List<string> history = new List<string>();
    private string input = "";
    private int historyIdx = -1, scroll, seenVersion = -1;
    private bool focusNext, cursorToEnd;
    private PlayerController lockedPlayer;
    private GUIStyle lineStyle, btnStyle, fieldStyle, statStyle;
    private Enemy[] statEnemies = new Enemy[0];
    private float statEnemiesTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; IsOpen = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null || FindFirstObjectByType<DebugConsole>() != null) return;
        new GameObject("DebugConsole").AddComponent<DebugConsole>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        if (!Application.isEditor && !Debug.isDebugBuild && !enableInRelease) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < CatOn.Length; i++) CatOn[i] = true;
        if (GetComponent<GameLogger>() == null) gameObject.AddComponent<GameLogger>();
        ConsoleCommands.RegisterAll(this);

        GameLog.Write(LogLevel.Info, LogCat.Console, "Debug console ready. Open: F1 or `   |   type 'help'   |   Tab = autocomplete, Up/Down = history, PgUp/PgDn = scroll");
        if (startOpen) SetOpen(true);
    }

    private void OnEnable() { if (toggleAction != null) toggleAction.action.Enable(); }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (IsOpen) SetOpen(false);
        Instance = null;
    }

    private void Update()
    {
        bool pressed;
        if (toggleAction != null) pressed = toggleAction.action.WasPressedThisFrame();
        else pressed = Keyboard.current != null && (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame);
        if (pressed) SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        if (open == IsOpen) return;
        IsOpen = open;

        if (open)
        {
            if (TargetedSpell.Active != null) TargetedSpell.Active.ForceCancel();   // never open mid-aim (game is frozen)
            var p = PlayerController.Instance;
            if (p != null) { p.LockActions(); p.LockMovement(); p.StopHorizontal(); lockedPlayer = p; }
            focusNext = true;
        }
        else if (lockedPlayer != null)
        {
            lockedPlayer.UnlockActions();
            lockedPlayer.UnlockMovement();
            lockedPlayer = null;
        }
        GameLog.Write(LogLevel.Trace, LogCat.Console, open ? "console opened (player input locked)" : "console closed (player input unlocked)");
    }

    public string SaveLog(string fileName)
    {
        string root = Application.isEditor ? Application.dataPath : Application.persistentDataPath;
        string n = string.IsNullOrWhiteSpace(fileName) ? "log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") : Path.GetFileName(fileName.Trim());
        if (!n.EndsWith(".txt")) n += ".txt";
        string path = Path.Combine(root, logFolder, n);
        int lines = GameLog.Save(path);
        return $"Saved {lines} lines to {path}";
    }

    // =====================  DRAWING  =====================

    private void OnGUI()
    {
        float s = Mathf.Max(0.5f, Screen.height / 1080f) * uiScale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float W = Screen.width / s, H = Screen.height / s;
        float panelH = IsOpen ? H * heightFraction : 0f;

        EnsureStyles();
        if (ShowStats && Event.current.type == EventType.Repaint) DrawStats(W, panelH);
        if (!IsOpen) return;

        Event e = Event.current;
        HandleKeys(e);
        if (e.type == EventType.ScrollWheel && e.mousePosition.y < panelH) { scroll -= (int)Mathf.Sign(e.delta.y) * 3; e.Use(); }

        if (seenVersion != GameLog.Version) Rebuild();

        // background
        GUI.color = new Color(0.04f, 0.04f, 0.07f, backgroundAlpha);
        GUI.DrawTexture(new Rect(0, 0, W, panelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // filter buttons
        float x = 6f, y = 4f, rowH = 22f;
        foreach (LogCat c in Enum.GetValues(typeof(LogCat)))
        {
            if (x + 76f > W - 6f) { x = 6f; y += rowH; }
            bool on = CatOn[(int)c];
            bool now = GUI.Toggle(new Rect(x, y, 76f, rowH - 2f), on, c.ToString(), btnStyle);
            if (now != on) { CatOn[(int)c] = now; MarkDirty(); }
            x += 78f;
        }
        if (x + 120f > W - 6f) { x = 6f; y += rowH; }
        if (GUI.Button(new Rect(x, y, 120f, rowH - 2f), "min: " + MinLevel, btnStyle))
        {
            MinLevel = MinLevel >= LogLevel.Critical ? LogLevel.Trace : MinLevel + 1;
            if (MinLevel == LogLevel.Success) MinLevel = LogLevel.Warning;   // Success is a command result, not a severity
            MarkDirty();
        }
        y += rowH + 2f;

        // log lines (anchored to the bottom)
        const float lineH = 17f;
        float top = y, bottom = panelH - 54f;
        int fit = Mathf.Max(1, (int)((bottom - top) / lineH));
        int total = visible.Count;
        scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, total - fit));
        int end = total - scroll, start = Mathf.Max(0, end - fit);

        if (e.type == EventType.Repaint)
        {
            for (int i = start; i < end; i++)
            {
                LogEntry en = visible[i];
                float ly = bottom - (end - i) * lineH;
                GUI.contentColor = new Color(0.5f, 0.5f, 0.55f);
                GUI.Label(new Rect(6f, ly, 120f, lineH), en.Clock + " f" + en.frame, lineStyle);
                GUI.contentColor = Color.HSVToRGB(((int)en.cat * 0.083f) % 1f, 0.4f, 0.95f);
                GUI.Label(new Rect(132f, ly, 70f, lineH), en.cat.ToString(), lineStyle);
                GUI.contentColor = GameLog.ColorOf(en.level);
                GUI.Label(new Rect(206f, ly, W - 212f, lineH), en.msg, lineStyle);
            }
            GUI.contentColor = Color.white;
        }
        if (scroll > 0)
        {
            GUI.contentColor = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(W - 260f, bottom + 1f, 255f, lineH), $"scrolled up {scroll} lines - End = jump to latest", lineStyle);
            GUI.contentColor = Color.white;
        }

        // usage hint + input
        GUI.contentColor = new Color(0.7f, 0.8f, 1f, 0.9f);
        GUI.Label(new Rect(8f, panelH - 50f, W - 16f, 20f), CommandRegistry.Hint(input), lineStyle);
        GUI.contentColor = Color.white;

        GUI.Label(new Rect(8f, panelH - 28f, 16f, 24f), ">", fieldStyle);
        GUI.SetNextControlName("ConsoleInput");
        input = GUI.TextField(new Rect(24f, panelH - 28f, W - 32f, 24f), input, fieldStyle);
        if (focusNext && e.type == EventType.Repaint) { GUI.FocusControl("ConsoleInput"); focusNext = false; }
        if (cursorToEnd)
        {
            var te = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (te != null) { te.text = input; te.MoveTextEnd(); }
            cursorToEnd = false;
        }
    }

    private void HandleKeys(Event e)
    {
        if (e.type != EventType.KeyDown) return;
        if (e.character == '`' || e.character == '§') { e.Use(); return; }   // the toggle key must not get typed

        switch (e.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter: Submit(); e.Use(); break;
            case KeyCode.UpArrow: History(-1); e.Use(); break;
            case KeyCode.DownArrow: History(1); e.Use(); break;
            case KeyCode.Tab: Complete(); e.Use(); break;
            case KeyCode.PageUp: scroll += 10; e.Use(); break;
            case KeyCode.PageDown: scroll = Mathf.Max(0, scroll - 10); e.Use(); break;
            case KeyCode.End: scroll = 0; e.Use(); break;
            case KeyCode.Escape: SetOpen(false); e.Use(); break;
        }
    }

    private void Submit()
    {
        string line = input.Trim();
        input = ""; historyIdx = -1; scroll = 0;
        if (line.Length == 0) return;
        if (history.Count == 0 || history[history.Count - 1] != line) history.Add(line);
        CommandRegistry.Execute(line);
    }

    private void History(int dir)
    {
        if (history.Count == 0) return;
        if (dir < 0) historyIdx = historyIdx < 0 ? history.Count - 1 : Mathf.Max(0, historyIdx - 1);
        else if (historyIdx >= 0) historyIdx++;
        if (historyIdx >= history.Count || historyIdx < 0) { historyIdx = -1; input = ""; }
        else input = history[historyIdx];
        cursorToEnd = true;
    }

    private void Complete()
    {
        string result = CommandRegistry.Complete(input, out List<string> matches);
        if (matches.Count > 1) GameLog.Write(LogLevel.Info, LogCat.Console, "Matches: " + string.Join(", ", matches));
        input = result;
        cursorToEnd = true;
    }

    private void Rebuild()
    {
        seenVersion = GameLog.Version;
        visible.Clear();
        foreach (var en in GameLog.Entries)
        {
            if (en.cat != LogCat.Console && (!CatOn[(int)en.cat] || en.level < MinLevel)) continue;   // console output is never hidden
            visible.Add(en);
        }
    }

    private void DrawStats(float W, float panelH)
    {
        var sb = new StringBuilder();
        sb.Append($"FPS {1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f):0}  frame {Time.frameCount}  timeScale {Time.timeScale:0.##}");
        if (Dev.AiFrozen) sb.Append("  [AI FROZEN]");

        var p = Dev.Player;
        if (p == null) sb.Append("\nPLAYER: none");
        else
        {
            var h = Dev.PlayerHealthOrNull; var l = Dev.PlayerLightOrNull; var caster = Dev.CasterOrNull;
            sb.Append($"\nPLAYER pos {Dev.V(p.transform.position)} vel {Dev.V(p.Rb.linearVelocity)} {(p.IsGrounded ? "grounded" : "AIR")} facing {(p.FacingSign > 0 ? "R" : "L")}");
            if (h != null) sb.Append($"\n  HP {h.Current:0.#}/{h.Max:0.#}{(h.IsInvulnerable ? " [invulnerable]" : "")}{(h.IsAlive ? "" : " [DEAD]")}");
            if (l != null) sb.Append($"   Light {l.Current:0.#}/{l.Max:0.#}");
            sb.Append($"\n  locks: move {Dev.Int(p, "movementLocks")} action {Dev.Int(p, "actionLocks")} gravity {Dev.Int(p, "gravityLocks")}   anim {(p.Anim != null ? p.Anim.Current.ToString() : "-")}");
            if (TargetedSpell.Active != null) sb.Append($"\n  AIMING: {TargetedSpell.Active.Data.DisplayName}");
            if (caster != null)
            {
                sb.Append("\n  spells:");
                for (int i = 0; i < caster.SlotCount; i++)
                {
                    var sp = caster.GetSpell(i);
                    sb.Append($" [{i + 1}] {(sp == null ? "-" : sp.Data.DisplayName + (sp.IsOnCooldown ? $" cd {sp.CooldownRemaining:0.0}" : " ready"))}");
                }
            }
        }

        if (Time.unscaledTime > statEnemiesTime) { statEnemies = Dev.Enemies(); statEnemiesTime = Time.unscaledTime + 0.5f; }
        int alive = 0; foreach (var en in statEnemies) if (en != null && !en.IsDead) alive++;
        sb.Append($"\nENEMIES alive {alive}/{statEnemies.Length}");
        if (p != null)
        {
            Enemy n = null; float bd = float.MaxValue;
            foreach (var en in statEnemies)
            {
                if (en == null || en.IsDead) continue;
                float d = Vector2.Distance(p.transform.position, en.transform.position);
                if (d < bd) { bd = d; n = en; }
            }
            if (n != null) sb.Append($"\n  nearest {Dev.Tag(n)} d={bd:0.0} HP {n.Health.Current:0.#}/{n.Health.Max:0.#}{(n.IsAttacking ? " ATTACKING" : "")}{(n.IsStunned ? " STUNNED" : "")}{(n.IsDormant ? " DORMANT" : "")}");
        }

        var content = new GUIContent(sb.ToString());
        Vector2 size = statStyle.CalcSize(content);
        var r = new Rect(W - size.x - 16f, panelH + 4f, size.x + 12f, size.y + 8f);
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 6f, r.y + 4f, size.x + 2f, size.y), content, statStyle);
    }

    private void EnsureStyles()
    {
        if (lineStyle != null) return;
        lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 15 };
        statStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false, normal = { textColor = new Color(0.85f, 1f, 0.85f) } };
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

public enum LogLevel { Trace, Info, Success, Warning, Error, Critical, Command }
public enum LogCat { System, Input, Player, Ability, Anim, Spell, Light, Combat, Enemy, Boss, Scene, Console, Interact }

public struct LogEntry
{
    public float time; public int frame; public LogLevel level; public LogCat cat; public string msg;
    public string Clock
    {
        get { int m = (int)(time / 60f); return m.ToString("00") + ":" + (time - m * 60f).ToString("00.000", CultureInfo.InvariantCulture); }
    }
}

/// <summary>The one log everything writes to. Main thread only.</summary>
public static class GameLog
{
    public const int MaxEntries = 6000;
    public static readonly List<LogEntry> Entries = new List<LogEntry>();
    public static int Version { get; private set; }   // bumps on every change (console redraw check)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Entries.Clear(); Version = 0; }

    public static void Write(LogLevel level, LogCat cat, string msg)
    {
        if (Entries.Count >= MaxEntries) Entries.RemoveRange(0, 1000);
        Entries.Add(new LogEntry { time = Time.realtimeSinceStartup, frame = Time.frameCount, level = level, cat = cat, msg = msg });
        Version++;
    }

    public static void Clear() { Entries.Clear(); Version++; }

    public static Color ColorOf(LogLevel l)
    {
        switch (l)
        {
            case LogLevel.Trace: return new Color(0.55f, 0.55f, 0.6f);
            case LogLevel.Success: return new Color(0.42f, 1f, 0.55f);
            case LogLevel.Warning: return new Color(1f, 0.85f, 0.3f);     // yellow
            case LogLevel.Error: return new Color(1f, 0.6f, 0.25f);       // orange
            case LogLevel.Critical: return new Color(1f, 0.22f, 0.22f);   // red
            case LogLevel.Command: return new Color(0.45f, 0.85f, 1f);    // cyan
            default: return new Color(0.9f, 0.9f, 0.9f);
        }
    }

    public static string Line(LogEntry e) => $"[{e.Clock} f{e.frame}] [{e.level}] [{e.cat}] {e.msg}";

    public static int Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var lines = new string[Entries.Count];
        for (int i = 0; i < lines.Length; i++) lines[i] = Line(Entries[i]);
        File.WriteAllLines(path, lines);
        return lines.Length;
    }
}

/// <summary>Small shared helpers for the console, the logger and commands.</summary>
public static class Dev
{
    private static readonly Dictionary<int, string> tags = new Dictionary<int, string>();
    private static int counter;
    public static bool AiFrozen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { tags.Clear(); counter = 0; AiFrozen = false; }

    public static PlayerController Player => PlayerController.Instance;
    public static Health PlayerHealthOrNull => Player != null ? Player.GetComponent<Health>() : null;
    public static PlayerLight PlayerLightOrNull => Player != null ? Player.GetComponent<PlayerLight>() : null;
    public static SpellCaster CasterOrNull => Player != null ? Player.GetComponentInChildren<SpellCaster>() : null;

    public static Enemy[] Enemies() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

    /// <summary>Unique readable name: "Abomination#3".</summary>
    public static string Tag(Enemy e)
    {
        if (e == null) return "?";
        int id = e.GetInstanceID();
        if (!tags.TryGetValue(id, out string t))
        {
            string n = e.name.Replace("(Clone)", "").Trim().Replace(' ', '_');
            t = n + "#" + (++counter);
            tags[id] = t;
        }
        return t;
    }

    public static string NameOf(GameObject go)
    {
        if (go == null) return "unknown";
        if (go.GetComponentInParent<PlayerController>() != null) return "Player";
        var e = go.GetComponentInParent<Enemy>();
        return e != null ? Tag(e) : go.name;
    }

    public static string NameOf(Health h) => h == null ? "?" : NameOf(h.gameObject);

    public static Enemy Nearest(Vector2 from, bool aliveOnly = true)
    {
        Enemy best = null; float bd = float.MaxValue;
        foreach (var e in Enemies())
        {
            if (aliveOnly && e.IsDead) continue;
            float d = Vector2.Distance(from, e.transform.position);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// <summary>Reads a private int field (lock counters) without editing those scripts.</summary>
    public static int Int(object o, string field)
    {
        if (o == null) return 0;
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        return f != null && f.GetValue(o) is int v ? v : 0;
    }

    public static string V(Vector2 v) => $"({v.x:0.##}, {v.y:0.##})";
}
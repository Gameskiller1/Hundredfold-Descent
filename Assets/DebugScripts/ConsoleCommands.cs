using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =====================================================================
//  EDIT NAMES HERE. Every command word and every argument word the console
//  understands lives in this class. Rename a string and it changes everywhere
//  (help, usage hints, autocomplete). Arrays = several words for the same thing.
// =====================================================================
public static class CmdNames
{
    // ---- command names ----
    public static string Help = "help", Clear = "clear", SaveLog = "savelog", Filter = "filter", LogLevel = "loglevel",
        Stats = "stats", TimeScale = "timescale", God = "god", Heal = "heal", Hurt = "hurt", Kill = "kill",
        Revive = "revive", Give = "give", Set = "set", Spawn = "spawn", Teleport = "tp", Equip = "equip",
        Unequip = "unequip", List = "list", Inspect = "inspect", Freeze = "freeze", Scene = "scene",
        Checkpoint = "checkpoint", Alias = "alias";

    // ---- stat words used by 'give' and 'set' ----
    public static string[] StatLight = { "light", "mana" };
    public static string[] StatHealth = { "health", "hp" };
    public static string[] StatMaxLight = { "maxlight", "maxmana" };
    public static string[] StatMaxHealth = { "maxhealth", "maxhp" };
    public static string[] StatSpeed = { "speed", "movespeed" };

    // ---- targets used by 'kill' / 'inspect' ----
    public static string[] TargetPlayer = { "player", "me" };
    public static string[] TargetAll = { "all", "enemies" };
    public static string[] TargetNearest = { "nearest", "closest" };

    // ---- 'list' topics ----
    public static string[] ListEnemies = { "enemies" };
    public static string[] ListSpells = { "spells" };
    public static string[] ListSpawnables = { "spawnables", "entities" };
    public static string[] ListCommands = { "commands" };
    public static string[] ListAliases = { "aliases" };

    // ---- on / off words ----
    public static string[] On = { "on", "true", "1", "yes" };
    public static string[] Off = { "off", "false", "0", "no" };

    public static bool Is(string v, string[] set)
    {
        foreach (var s in set) if (string.Equals(s, v, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

public enum ArgType { Text, Int, Float, Bool, Coord }

public class Arg
{
    public readonly string name; public readonly ArgType type; public readonly bool required;
    public readonly Func<IEnumerable<string>> choices;
    public Arg(string name, ArgType type, bool required, Func<IEnumerable<string>> choices = null)
    { this.name = name; this.type = type; this.required = required; this.choices = choices; }
}

public class CommandError : Exception { public CommandError(string m) : base(m) { } }

public class Command
{
    public readonly string Name, Description; public readonly Arg[] Args; public readonly Func<CommandContext, string> Run;
    public readonly List<string> Aliases = new List<string>();
    public bool IsVariadic { get; private set; }

    public Command(string name, string description, Func<CommandContext, string> run, params Arg[] args)
    { Name = name; Description = description; Run = run; Args = args; }

    public Command Alias(params string[] a) { Aliases.AddRange(a); return this; }
    public Command Variadic() { IsVariadic = true; return this; }

    public string Usage()
    {
        var sb = new StringBuilder(Name);
        foreach (var a in Args) sb.Append(a.required ? $" <{a.name}>" : $" [{a.name}]");
        return sb.ToString();
    }

    public void Validate(string[] args)
    {
        int required = 0;
        for (int i = 0; i < Args.Length; i++) if (Args[i].required) required = i + 1;
        if (args.Length < required) throw new CommandError($"Missing required argument <{Args[args.Length].name}>.");
        if (!IsVariadic && args.Length > Args.Length) throw new CommandError($"Too many arguments (this command takes {Args.Length}).");

        for (int i = 0; i < Math.Min(Args.Length, args.Length); i++)
        {
            var a = Args[i]; string v = args[i];
            bool ok = true;
            switch (a.type)
            {
                case ArgType.Int: ok = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _); break;
                case ArgType.Float: ok = ArgParse.TryFloat(v, out _); break;
                case ArgType.Bool: ok = CmdNames.Is(v, CmdNames.On) || CmdNames.Is(v, CmdNames.Off); break;
                case ArgType.Coord: ok = ArgParse.TryCoord(v, out _, out _); break;
            }
            if (!ok) throw new CommandError($"Argument <{a.name}> must be {a.type.ToString().ToLower()} (got '{v}').{(a.type == ArgType.Coord ? " Use a number, ~ (here) or ~2 (here + 2)." : "")}");
        }
    }
}

public static class ArgParse
{
    public static bool TryFloat(string s, out float f) => float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

    public static bool TryCoord(string s, out float value, out bool relative)
    {
        relative = s.StartsWith("~"); value = 0f;
        if (!relative) return TryFloat(s, out value);
        return s.Length == 1 || TryFloat(s.Substring(1), out value);
    }
}

public class CommandContext
{
    public readonly string[] Args;
    public CommandContext(string[] args) { Args = args; }

    public bool Has(int i) => i < Args.Length;
    public string Str(int i, string def = null) => Has(i) ? Args[i] : def;
    public string Rest(int from) => from >= Args.Length ? "" : string.Join(" ", Args, from, Args.Length - from);
    public int Int(int i, int def) => Has(i) ? int.Parse(Args[i], CultureInfo.InvariantCulture) : def;
    public float Float(int i, float def) { if (!Has(i)) return def; ArgParse.TryFloat(Args[i], out float f); return f; }
    public bool Bool(int i, bool def) => !Has(i) ? def : CmdNames.Is(Args[i], CmdNames.On);
    public bool Toggle(int i, bool current) => Has(i) ? CmdNames.Is(Args[i], CmdNames.On) : !current;

    /// <summary>Number, "~" (origin) or "~2" (origin + 2). Missing = def.</summary>
    public float Coord(int i, float origin, float def)
    {
        if (!Has(i)) return def;
        ArgParse.TryCoord(Args[i], out float v, out bool rel);
        return rel ? origin + v : v;
    }
    public Vector2 Point(int i, Vector2 origin, Vector2 def) => new Vector2(Coord(i, origin.x, def.x), Coord(i + 1, origin.y, def.y));

    public void Info(string m) { foreach (var l in m.Split('\n')) GameLog.Write(LogLevel.Info, LogCat.Console, l); }
}

public static class CommandRegistry
{
    private static readonly Dictionary<string, Command> map = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Command> all = new List<Command>();
    private static readonly Dictionary<string, string> userAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Command> All => all;
    public static IReadOnlyDictionary<string, string> UserAliases => userAliases;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Clear(); }

    public static void Clear() { map.Clear(); all.Clear(); userAliases.Clear(); }

    public static void Add(Command c)
    {
        all.Add(c);
        map[c.Name] = c;
        foreach (var a in c.Aliases) map[a] = c;
    }

    public static Command Find(string word) => map.TryGetValue(word, out var c) ? c : null;

    public static void AddAlias(string alias, Command target) { map[alias] = target; userAliases[alias] = target.Name; }

    public static IEnumerable<string> Words() => map.Keys;

    public static void Execute(string line)
    {
        GameLog.Write(LogLevel.Command, LogCat.Console, "> " + line);
        var t = Tokenize(line);
        if (t.Count == 0) return;

        var cmd = Find(t[0]);
        if (cmd == null)
        {
            var sug = new List<string>();
            foreach (var w in map.Keys) if (w.StartsWith(t[0].Substring(0, 1), StringComparison.OrdinalIgnoreCase) || w.Contains(t[0])) sug.Add(w);
            Fail("Unknown command '" + t[0] + "'." + (sug.Count > 0 ? " Did you mean: " + string.Join(", ", sug.GetRange(0, Math.Min(6, sug.Count))) + "?" : " Type 'help'."));
            return;
        }

        t.RemoveAt(0);
        try
        {
            cmd.Validate(t.ToArray());
            string result = cmd.Run(new CommandContext(t.ToArray()));
            if (!string.IsNullOrEmpty(result))
                foreach (var l in result.Split('\n')) GameLog.Write(LogLevel.Success, LogCat.Console, l);
        }
        catch (CommandError ex) { Fail(ex.Message + "   Usage: " + cmd.Usage()); }
        catch (Exception ex) { GameLog.Write(LogLevel.Critical, LogCat.Console, $"Command '{cmd.Name}' crashed: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void Fail(string m) => GameLog.Write(LogLevel.Error, LogCat.Console, m);

    private static List<string> Tokenize(string line)
    {
        var res = new List<string>(); var sb = new StringBuilder(); bool q = false;
        foreach (char c in line)
        {
            if (c == '"') { q = !q; continue; }
            if (char.IsWhiteSpace(c) && !q) { if (sb.Length > 0) { res.Add(sb.ToString()); sb.Length = 0; } continue; }
            sb.Append(c);
        }
        if (sb.Length > 0) res.Add(sb.ToString());
        return res;
    }

    // ---- autocomplete + hints ----

    public static string Complete(string input, out List<string> matches)
    {
        matches = new List<string>();
        var tokens = new List<string>(input.Split(' '));
        int idx = tokens.Count - 1;
        string partial = tokens[idx];

        IEnumerable<string> pool = null;
        if (idx == 0) pool = Words();
        else
        {
            var cmd = Find(tokens[0]);
            if (cmd != null && idx - 1 < cmd.Args.Length && cmd.Args[idx - 1].choices != null) pool = cmd.Args[idx - 1].choices();
        }
        if (pool == null) return input;

        foreach (var w in pool) if (w.StartsWith(partial, StringComparison.OrdinalIgnoreCase)) matches.Add(w);
        if (matches.Count == 0) return input;
        matches.Sort();

        string common = matches[0];
        foreach (var m in matches) { int n = 0; while (n < common.Length && n < m.Length && char.ToLower(common[n]) == char.ToLower(m[n])) n++; common = common.Substring(0, n); }

        tokens[idx] = matches.Count == 1 ? common + " " : common;
        return string.Join(" ", tokens);
    }

    public static string Hint(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "type 'help' for commands";
        var tokens = input.Split(' ');
        var cmd = Find(tokens[0]);
        if (cmd != null && (tokens.Length > 1 || input.EndsWith(" ")))
            return cmd.Usage() + "   -   " + cmd.Description;

        var list = new List<string>();
        foreach (var w in map.Keys) if (w.StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase)) list.Add(w);
        list.Sort();
        if (cmd != null) return cmd.Usage() + "   -   " + cmd.Description;
        return list.Count == 0 ? "unknown command" : string.Join("   ", list.GetRange(0, Math.Min(10, list.Count)));
    }
}

// =====================================================================
//  THE COMMANDS. Add your own: CommandRegistry.Add(new Command(name, description, ctx => "result", args...));
//  Throw new CommandError("why") to fail. Return a string to print it as a success.
// =====================================================================
public static class ConsoleCommands
{
    private static readonly HashSet<Enemy> frozen = new HashSet<Enemy>();
    private static bool godOn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frozen.Clear(); godOn = false; }

    private static PlayerController P { get { var p = PlayerController.Instance; if (p == null) throw new CommandError("There is no player in the scene."); return p; } }
    private static Health PH { get { var h = P.GetComponent<Health>(); if (h == null) throw new CommandError("The player has no Health component."); return h; } }
    private static PlayerLight PL { get { var l = P.GetComponent<PlayerLight>(); if (l == null) throw new CommandError("The player has no PlayerLight component."); return l; } }
    private static SpellCaster Caster { get { var c = Dev.CasterOrNull; if (c == null) throw new CommandError("The player has no SpellCaster."); return c; } }
    private static string Pt(Vector2 v) => Dev.V(v);

    public static void RegisterAll(DebugConsole con)
    {
        CommandRegistry.Clear();

        // ---------- console ----------
        CommandRegistry.Add(new Command(CmdNames.Help, "List commands, or show one command's usage.", ctx =>
        {
            if (ctx.Has(0))
            {
                var c = CommandRegistry.Find(ctx.Str(0)) ?? throw new CommandError("No command called '" + ctx.Str(0) + "'.");
                ctx.Info(c.Usage() + "\n  " + c.Description + (c.Aliases.Count > 0 ? "\n  aliases: " + string.Join(", ", c.Aliases) : ""));
                return null;
            }
            foreach (var c in CommandRegistry.All) ctx.Info($"{c.Usage(),-42} {c.Description}");
            ctx.Info("<arg> = required, [arg] = optional.  Coordinates: 5 3 = world, ~ ~2 = relative to the player.");
            return null;
        }, new Arg("command", ArgType.Text, false, () => CommandRegistry.Words())).Alias("?"));

        CommandRegistry.Add(new Command(CmdNames.Clear, "Clear the log.", ctx => { GameLog.Clear(); return "Log cleared."; }).Alias("cls"));

        CommandRegistry.Add(new Command(CmdNames.SaveLog, "Save the whole log to a text file (folder is set on the DebugConsole).", ctx => con.SaveLog(ctx.Str(0)),
            new Arg("filename", ArgType.Text, false)));

        CommandRegistry.Add(new Command(CmdNames.Filter, "Show/hide a log category. No arguments = show status.", ctx =>
        {
            var cats = (LogCat[])Enum.GetValues(typeof(LogCat));
            if (!ctx.Has(0))
            {
                var sb = new StringBuilder("Categories:");
                foreach (var c in cats) sb.Append($" {c}={(con.CatOn[(int)c] ? "on" : "off")}");
                return sb.ToString();
            }
            string w = ctx.Str(0);
            bool all = w == "all", none = w == "none";
            if (all || none) { for (int i = 0; i < con.CatOn.Length; i++) con.CatOn[i] = all; con.MarkDirty(); return all ? "All categories shown." : "All categories hidden."; }
            foreach (var c in cats)
                if (string.Equals(c.ToString(), w, StringComparison.OrdinalIgnoreCase))
                {
                    con.CatOn[(int)c] = ctx.Toggle(1, con.CatOn[(int)c]); con.MarkDirty();
                    return $"{c} is now {(con.CatOn[(int)c] ? "shown" : "hidden")}.";
                }
            throw new CommandError("Unknown category '" + w + "'.");
        }, new Arg("category", ArgType.Text, false, () => CatNames()), new Arg("on|off", ArgType.Bool, false)));

        CommandRegistry.Add(new Command(CmdNames.LogLevel, "Hide everything below this severity (Trace, Info, Warning, Error, Critical).", ctx =>
        {
            if (!ctx.Has(0)) return "Minimum level: " + con.MinLevel;
            if (!Enum.TryParse(ctx.Str(0), true, out LogLevel lv) || lv == LogLevel.Command || lv == LogLevel.Success) throw new CommandError("Use Trace, Info, Warning, Error or Critical.");
            con.MinLevel = lv; con.MarkDirty(); return "Minimum level: " + lv;
        }, new Arg("level", ArgType.Text, false, () => new[] { "Trace", "Info", "Warning", "Error", "Critical" })));

        CommandRegistry.Add(new Command(CmdNames.Stats, "Live stats panel (player, spells, nearest enemy).", ctx =>
        { con.ShowStats = ctx.Toggle(0, con.ShowStats); return "Stats panel " + (con.ShowStats ? "on." : "off."); },
            new Arg("on|off", ArgType.Bool, false)));

        // ---------- world ----------
        CommandRegistry.Add(new Command(CmdNames.TimeScale, "Game speed. 1 = normal, 0.2 = slow-mo, 0 = paused (console still works).", ctx =>
        {
            if (!ctx.Has(0)) return "timeScale = " + Time.timeScale;
            float v = ctx.Float(0, 1f);
            if (v < 0f || v > 100f) throw new CommandError("Value must be 0-100.");
            Time.timeScale = v; return "timeScale = " + v;
        }, new Arg("value", ArgType.Float, false)));

        CommandRegistry.Add(new Command(CmdNames.Scene, "Load a scene (no argument = reload the current one).", ctx =>
        {
            string n = ctx.Str(0, SceneManager.GetActiveScene().name);
            if (!Application.CanStreamedLevelBeLoaded(n)) throw new CommandError($"Scene '{n}' is not in Build Settings.");
            SceneManager.LoadScene(n); return "Loading scene " + n;
        }, new Arg("name", ArgType.Text, false)));

        CommandRegistry.Add(new Command(CmdNames.Freeze, "Freeze / unfreeze all enemy AI (running attacks finish).", ctx =>
        {
            bool on = ctx.Toggle(0, Dev.AiFrozen);
            Dev.AiFrozen = on;
            int n = 0;
            if (on) { foreach (var e in Dev.Enemies()) if (frozen.Add(e)) { e.LockAI(); n++; } }
            else { foreach (var e in frozen) if (e != null) { e.UnlockAI(); n++; } frozen.Clear(); }
            return $"Enemy AI {(on ? "frozen" : "unfrozen")} ({n} enemies).";
        }, new Arg("on|off", ArgType.Bool, false)));

        CommandRegistry.Add(new Command(CmdNames.Spawn, "Spawn an enemy / prefab. Coordinates optional (default: in front of the player).", ctx =>
        {
            var prefab = FindSpawnable(con, ctx.Str(0)) ?? throw new CommandError($"No spawnable '{ctx.Str(0)}'. Use 'list {CmdNames.ListSpawnables[0]}' or add prefabs to the DebugConsole.");
            var p = Dev.Player;
            Vector2 origin = p != null ? (Vector2)p.transform.position : Vector2.zero;
            Vector2 def = origin + new Vector2((p != null ? p.FacingSign : 1) * 2.5f, 0.5f);
            Vector2 at = ctx.Point(1, origin, def);
            int count = Mathf.Clamp(ctx.Int(3, 1), 1, 50);
            for (int i = 0; i < count; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefab, at + new Vector2(i * 1.2f, 0f), Quaternion.identity);
                go.name = prefab.name.Replace("(Clone)", "").Trim();
                var e = go.GetComponent<Enemy>();
                if (e != null && Dev.AiFrozen && frozen.Add(e)) e.LockAI();
            }
            return $"Spawned {count} x {prefab.name} at {Pt(at)}";
        }, new Arg("entity", ArgType.Text, true, () => SpawnNames(con)), new Arg("x", ArgType.Coord, false), new Arg("y", ArgType.Coord, false), new Arg("count", ArgType.Int, false)));

        CommandRegistry.Add(new Command(CmdNames.Kill, "Kill the player, all enemies, the nearest enemy, or one by name/tag.", ctx =>
        {
            string t = ctx.Str(0);
            if (CmdNames.Is(t, CmdNames.TargetPlayer)) { PH.Kill(); return "Player killed."; }
            if (CmdNames.Is(t, CmdNames.TargetAll))
            {
                int n = 0; foreach (var e in Dev.Enemies()) if (!e.IsDead) { e.Health.Kill(); n++; }
                return $"Killed {n} enemies.";
            }
            var target = FindEnemy(t);
            target.Health.Kill(); return "Killed " + Dev.Tag(target);
        }, new Arg("target", ArgType.Text, true, () => Targets())));

        // ---------- player ----------
        CommandRegistry.Add(new Command(CmdNames.God, "Player can't be hurt.", ctx =>
        {
            bool on = ctx.Toggle(0, godOn);
            if (on && !godOn) { PH.LockInvulnerable(); godOn = true; }
            else if (!on && godOn) { PH.UnlockInvulnerable(); godOn = false; }
            return "God mode " + (godOn ? "ON" : "OFF");
        }, new Arg("on|off", ArgType.Bool, false)));

        CommandRegistry.Add(new Command(CmdNames.Heal, "Heal the player (no amount = full).", ctx =>
        {
            var h = PH; if (!h.IsAlive) throw new CommandError($"Player is dead. Use '{CmdNames.Revive}'.");
            h.Heal(ctx.Has(0) ? ctx.Float(0, 0f) : h.Max); return $"HP {h.Current:0.#}/{h.Max:0.#}";
        }, new Arg("amount", ArgType.Float, false)));

        CommandRegistry.Add(new Command(CmdNames.Hurt, "Damage the player (ignores invulnerability).", ctx =>
        {
            var h = PH; float a = ctx.Float(0, 10f);
            h.TakeDamage(new DamageInfo(a, Faction.Neutral, null, DamageType.Pure) { ignoreInvulnerability = true });
            return $"Player took {a:0.#}. HP {h.Current:0.#}/{h.Max:0.#}";
        }, new Arg("amount", ArgType.Float, false)));

        CommandRegistry.Add(new Command(CmdNames.Revive, "Respawn the player at the checkpoint.", ctx =>
        {
            var ph = P.GetComponent<PlayerHealth>() ?? throw new CommandError("The player has no PlayerHealth component.");
            if (!ph.IsDead) throw new CommandError("The player isn't dead.");
            ph.Respawn(); return "Respawned at " + Pt(ph.Checkpoint);
        }));

        CommandRegistry.Add(new Command(CmdNames.Checkpoint, "Set the respawn point (default: where the player stands).", ctx =>
        {
            var ph = P.GetComponent<PlayerHealth>() ?? throw new CommandError("The player has no PlayerHealth component.");
            Vector2 pos = P.transform.position; Vector2 at = ctx.Point(0, pos, pos);
            ph.SetCheckpoint(at); return "Checkpoint = " + Pt(at);
        }, new Arg("x", ArgType.Coord, false), new Arg("y", ArgType.Coord, false)));

        CommandRegistry.Add(new Command(CmdNames.Teleport, "Teleport the player. No coordinates = to the mouse cursor.", ctx =>
        {
            Vector2 pos = P.transform.position, at;
            if (!ctx.Has(0))
            {
                if (Camera.main == null || Mouse.current == null) throw new CommandError("No main camera / mouse.");
                Vector2 s = Mouse.current.position.ReadValue();
                at = Camera.main.ScreenToWorldPoint(new Vector3(s.x, s.y, -Camera.main.transform.position.z));
            }
            else at = ctx.Point(0, pos, pos);
            P.transform.position = at; P.StopImmediately();
            return "Teleported to " + Pt(at);
        }, new Arg("x", ArgType.Coord, false), new Arg("y", ArgType.Coord, false)).Alias("teleport"));

        CommandRegistry.Add(new Command(CmdNames.Give, "Give the player something (default amount 25).", ctx =>
        {
            string w = ctx.Str(0); float a = ctx.Float(1, 25f);
            if (CmdNames.Is(w, CmdNames.StatLight)) { PL.Add(a); return $"Light {PL.Current:0.#}/{PL.Max:0.#}"; }
            if (CmdNames.Is(w, CmdNames.StatHealth)) { PH.Heal(a); return $"HP {PH.Current:0.#}/{PH.Max:0.#}"; }
            throw new CommandError("Can't give '" + w + "'. Options: " + string.Join(", ", GiveWords()));
        }, new Arg("what", ArgType.Text, true, () => GiveWords()), new Arg("amount", ArgType.Float, false)));

        CommandRegistry.Add(new Command(CmdNames.Set, "Set a stat to an exact value.", ctx =>
        {
            string w = ctx.Str(0); float v = ctx.Float(1, 0f);
            if (CmdNames.Is(w, CmdNames.StatLight)) { PL.Add(v - PL.Current); return $"Light = {PL.Current:0.#}"; }
            if (CmdNames.Is(w, CmdNames.StatMaxLight)) { PL.SetMax(v, true); return $"Max light = {PL.Max:0.#}"; }
            if (CmdNames.Is(w, CmdNames.StatMaxHealth)) { PH.SetMax(v, true); return $"Max HP = {PH.Max:0.#}"; }
            if (CmdNames.Is(w, CmdNames.StatHealth)) { var h = PH; if (v > h.Current) h.Heal(v - h.Current); else h.TakeDamage(new DamageInfo(h.Current - v, Faction.Neutral, null, DamageType.Pure) { ignoreInvulnerability = true }); return $"HP = {h.Current:0.#}"; }
            if (CmdNames.Is(w, CmdNames.StatSpeed)) { P.MoveSpeed = v; return "Move speed = " + v; }
            throw new CommandError("Can't set '" + w + "'. Options: " + string.Join(", ", SetWords()));
        }, new Arg("stat", ArgType.Text, true, () => SetWords()), new Arg("value", ArgType.Float, true)));

        CommandRegistry.Add(new Command(CmdNames.Equip, "Put a spell in a slot (default: first empty slot).", ctx =>
        {
            var data = FindSpell(con, ctx.Str(0)) ?? throw new CommandError("No spell '" + ctx.Str(0) + "'. Try 'list " + CmdNames.ListSpells[0] + "'.");
            var c = Caster; int slot = ctx.Has(1) ? ctx.Int(1, 1) - 1 : -1;
            if (slot < 0) { slot = 0; for (int i = 0; i < c.SlotCount; i++) if (c.GetData(i) == null) { slot = i; break; } }
            if (slot >= c.SlotCount) throw new CommandError($"Slot must be 1-{c.SlotCount}.");
            c.Equip(slot, data); return $"Equipped {data.DisplayName} in slot {slot + 1}";
        }, new Arg("spell", ArgType.Text, true, () => SpellNames(con)), new Arg("slot", ArgType.Int, false)));

        CommandRegistry.Add(new Command(CmdNames.Unequip, "Empty a spell slot.", ctx =>
        {
            int slot = ctx.Int(0, 1) - 1; var c = Caster;
            if (slot < 0 || slot >= c.SlotCount) throw new CommandError($"Slot must be 1-{c.SlotCount}.");
            c.Unequip(slot); return "Slot " + (slot + 1) + " emptied.";
        }, new Arg("slot", ArgType.Int, true)));

        // ---------- info ----------
        CommandRegistry.Add(new Command(CmdNames.List, "List things: enemies, spells, spawnables, commands, aliases.", ctx =>
        {
            string w = ctx.Str(0, CmdNames.ListCommands[0]);
            if (CmdNames.Is(w, CmdNames.ListEnemies))
            {
                var en = Dev.Enemies(); if (en.Length == 0) return "No enemies in the scene.";
                foreach (var e in en) ctx.Info($"{Dev.Tag(e),-20} {Pt(e.transform.position),-16} HP {e.Health.Current:0.#}/{e.Health.Max:0.#}  {EnemyFlags(e)}");
                return null;
            }
            if (CmdNames.Is(w, CmdNames.ListSpells)) { foreach (var n in SpellNames(con)) ctx.Info(n); return null; }
            if (CmdNames.Is(w, CmdNames.ListSpawnables)) { foreach (var n in SpawnNames(con)) ctx.Info(n); return null; }
            if (CmdNames.Is(w, CmdNames.ListAliases)) { if (CommandRegistry.UserAliases.Count == 0) return "No custom aliases."; foreach (var kv in CommandRegistry.UserAliases) ctx.Info(kv.Key + " -> " + kv.Value); return null; }
            if (CmdNames.Is(w, CmdNames.ListCommands)) { foreach (var c in CommandRegistry.All) ctx.Info(c.Name); return null; }
            throw new CommandError("Unknown topic. Options: " + string.Join(", ", ListWords()));
        }, new Arg("what", ArgType.Text, false, () => ListWords())));

        CommandRegistry.Add(new Command(CmdNames.Inspect, "Dump everything about the player, the nearest enemy, or an enemy by name.", ctx =>
        {
            string t = ctx.Str(0, CmdNames.TargetNearest[0]);
            if (CmdNames.Is(t, CmdNames.TargetPlayer)) { InspectPlayer(ctx); return null; }
            InspectEnemy(ctx, FindEnemy(t)); return null;
        }, new Arg("target", ArgType.Text, false, () => Targets())));

        CommandRegistry.Add(new Command(CmdNames.Alias, "Make a new word for an existing command (this session).", ctx =>
        {
            var target = CommandRegistry.Find(ctx.Str(1)) ?? throw new CommandError("No command '" + ctx.Str(1) + "'.");
            CommandRegistry.AddAlias(ctx.Str(0), target); return $"'{ctx.Str(0)}' now runs {target.Name}";
        }, new Arg("newWord", ArgType.Text, true), new Arg("command", ArgType.Text, true, () => CommandRegistry.Words())));
    }

    // ---------- lookups ----------

    private static IEnumerable<string> CatNames() { var l = new List<string> { "all", "none" }; foreach (var c in Enum.GetNames(typeof(LogCat))) l.Add(c); return l; }
    private static IEnumerable<string> GiveWords() { var l = new List<string>(CmdNames.StatLight); l.AddRange(CmdNames.StatHealth); return l; }
    private static IEnumerable<string> SetWords() { var l = new List<string>(); l.AddRange(CmdNames.StatLight); l.AddRange(CmdNames.StatHealth); l.AddRange(CmdNames.StatMaxLight); l.AddRange(CmdNames.StatMaxHealth); l.AddRange(CmdNames.StatSpeed); return l; }
    private static IEnumerable<string> ListWords() { var l = new List<string>(); l.AddRange(CmdNames.ListEnemies); l.AddRange(CmdNames.ListSpells); l.AddRange(CmdNames.ListSpawnables); l.AddRange(CmdNames.ListCommands); l.AddRange(CmdNames.ListAliases); return l; }

    private static IEnumerable<string> Targets()
    {
        var l = new List<string>(); l.AddRange(CmdNames.TargetPlayer); l.AddRange(CmdNames.TargetAll); l.AddRange(CmdNames.TargetNearest);
        foreach (var e in Dev.Enemies()) l.Add(Dev.Tag(e));
        return l;
    }

    private static Enemy FindEnemy(string key)
    {
        Vector2 from = Dev.Player != null ? (Vector2)Dev.Player.transform.position : Vector2.zero;
        if (CmdNames.Is(key, CmdNames.TargetNearest)) return Dev.Nearest(from) ?? throw new CommandError("No living enemies.");
        Enemy best = null; float bd = float.MaxValue;
        foreach (var e in Dev.Enemies())
        {
            string tag = Dev.Tag(e);
            if (!string.Equals(tag, key, StringComparison.OrdinalIgnoreCase) && tag.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0) continue;
            float d = Vector2.Distance(from, e.transform.position);
            if (d < bd) { bd = d; best = e; }
        }
        return best ?? throw new CommandError("No enemy matching '" + key + "'. Try 'list " + CmdNames.ListEnemies[0] + "'.");
    }

    private static GameObject FindSpawnable(DebugConsole con, string key)
    {
        foreach (var g in con.Spawnables) if (g != null && string.Equals(g.name, key, StringComparison.OrdinalIgnoreCase)) return g;
        foreach (var g in con.Spawnables) if (g != null && g.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return g;
        foreach (var e in Dev.Enemies()) if (!e.IsDead && e.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return e.gameObject;   // clone a scene enemy
        return null;
    }

    private static IEnumerable<string> SpawnNames(DebugConsole con)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in con.Spawnables) if (g != null) set.Add(g.name);
        foreach (var e in Dev.Enemies()) set.Add(e.name.Replace("(Clone)", "").Trim());
        return set;
    }

    private static IEnumerable<SpellData> AllSpells(DebugConsole con)
    {
        var seen = new HashSet<SpellData>();
        foreach (var s in con.Spells) if (s != null && seen.Add(s)) yield return s;
        foreach (var s in Resources.FindObjectsOfTypeAll<SpellData>()) if (s != null && seen.Add(s)) yield return s;
    }

    private static string Key(string s) => s.Replace(" ", "").Replace("_", "").ToLowerInvariant();

    private static SpellData FindSpell(DebugConsole con, string key)
    {
        string k = Key(key);
        foreach (var s in AllSpells(con)) if (Key(s.name) == k || Key(s.name) == "spell" + k || Key(s.DisplayName) == k) return s;
        foreach (var s in AllSpells(con)) if (Key(s.name).Contains(k) || Key(s.DisplayName).Contains(k)) return s;
        return null;
    }

    private static IEnumerable<string> SpellNames(DebugConsole con)
    {
        var l = new List<string>();
        foreach (var s in AllSpells(con)) l.Add(s.name.StartsWith("Spell_") ? s.name.Substring(6) : s.name);
        return l;
    }

    // ---------- dumps ----------

    private static string EnemyFlags(Enemy e)
    {
        var f = new List<string>();
        if (e.IsDead) f.Add("DEAD"); if (e.IsDormant) f.Add("dormant"); if (e.IsStunned) f.Add("stunned");
        if (e.IsKnockedBack) f.Add("knockback"); if (e.IsAttacking) f.Add("attacking");
        var b = e.Brain as PatrolChaseBrain; if (b != null && b.IsAlerted) f.Add("alerted");
        return f.Count == 0 ? "idle" : string.Join(",", f);
    }

    private static void InspectPlayer(CommandContext ctx)
    {
        var p = P; var h = p.GetComponent<Health>(); var l = p.GetComponent<PlayerLight>();
        var sb = new StringBuilder("PLAYER");
        sb.Append($"\n  pos {Pt(p.transform.position)}  vel {Pt(p.Rb.linearVelocity)}  grounded {p.IsGrounded}  facing {p.FacingSign}  moveInput {p.MoveInput}");
        sb.Append($"\n  canAct {p.CanAct}  movementLocked {p.IsMovementLocked}  gravityLocked {p.IsGravityLocked}  locks: move {Dev.Int(p, "movementLocks")} action {Dev.Int(p, "actionLocks")} gravity {Dev.Int(p, "gravityLocks")}");
        if (h != null) sb.Append($"\n  HP {h.Current:0.#}/{h.Max:0.#}  alive {h.IsAlive}  invulnerable {h.IsInvulnerable}  faction {h.Faction}");
        if (l != null) sb.Append($"\n  Light {l.Current:0.#}/{l.Max:0.#}");
        if (p.Anim != null) sb.Append($"\n  animation {p.Anim.Current}  oneShot {p.Anim.IsPlayingOneShot}  dead {p.Anim.IsDead}");
        var c = p.GetComponentInChildren<SpellCaster>();
        if (c != null) for (int i = 0; i < c.SlotCount; i++) { var s = c.GetSpell(i); sb.Append($"\n  slot {i + 1}: {(s == null ? "(empty)" : $"{s.Data.DisplayName} cost {s.LightCost} cooldown {s.CooldownRemaining:0.00}s allowed {s.IsAllowed}")}"); }
        sb.Append($"\n  aiming {(TargetedSpell.Active != null ? TargetedSpell.Active.Data.DisplayName : "no")}  timeScale {Time.timeScale}");
        ctx.Info(sb.ToString());
    }

    private static void InspectEnemy(CommandContext ctx, Enemy e)
    {
        var sb = new StringBuilder(Dev.Tag(e).ToUpper());
        sb.Append($"\n  pos {Pt(e.transform.position)}  vel {Pt(e.Rb.linearVelocity)}  facing {e.FacingSign}  faction {e.Faction}");
        sb.Append($"\n  HP {e.Health.Current:0.#}/{e.Health.Max:0.#}  invulnerable {e.Health.IsInvulnerable}  speedx {e.SpeedMultiplier}  damagex {e.DamageMultiplier}");
        sb.Append($"\n  state: {EnemyFlags(e)}  canMove {e.CanMove}  canThink {e.CanThink}  locks: move {Dev.Int(e, "movementLocks")} ai {Dev.Int(e, "aiLocks")}");
        sb.Append($"\n  brain {(e.Brain != null ? e.Brain.GetType().Name : "none")}  motor {(e.Motor != null ? e.Motor.GetType().Name : "none")}  animation {(e.Anim != null ? e.Anim.Current : "-")}");
        sb.Append($"\n  target: {(e.HasTarget ? $"distance {e.DistanceToTarget:0.0}" : "none")}");
        foreach (var a in e.Attacks)
            if (a != null) sb.Append($"\n  attack {a.name} ({a.GetType().Name}) enabled {a.enabled} running {a.IsRunning} offCooldown {a.IsOffCooldown} inRange {(e.HasTarget && a.InRange(e.TargetCenter))}");
        var boss = e.GetComponent<BossPhases>();
        if (boss != null) sb.Append($"\n  boss phase {boss.CurrentIndex + 1}/{boss.PhaseCount} {(boss.Current != null ? boss.Current.name : "")} transitioning {boss.IsTransitioning}");
        ctx.Info(sb.ToString());
    }
}
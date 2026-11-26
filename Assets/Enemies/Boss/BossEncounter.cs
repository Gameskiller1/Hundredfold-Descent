using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A boss fight in an arena. Put on an object with a trigger Collider2D covering the arena entrance.
/// The boss sleeps (dormant) until the player walks in. Then: events fire (close doors), the boss bar
/// shows, the boss wakes. Boss dies -> events (open doors, drops). Player dies -> boss resets.
/// Give the encounter a unique Boss Id to keep a beaten boss from respawning when you re-enter the scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossEncounter : MonoBehaviour
{
    [SerializeField] private Enemy boss;
    [Tooltip("Unique per boss (e.g. 'castle_warden'). Empty = the boss comes back whenever the scene loads.")]
    [SerializeField] private string bossId;

    [Header("Start")]
    [Tooltip("Pause between the player entering and the boss waking up (cutscene, intro roar).")]
    [SerializeField, Min(0f)] private float startDelay = 0.5f;

    [Header("UI (optional)")]
    [SerializeField] private HealthBarUI bossBar;

    [Header("Reset")]
    [Tooltip("Player died -> boss heals, goes home and the fight can be triggered again.")]
    [SerializeField] private bool resetWhenPlayerDies = true;

    [Header("Events")]
    [Tooltip("Close the doors, start the music...")]
    [SerializeField] private UnityEvent onStarted;
    [Tooltip("Open the doors, drop the reward...")]
    [SerializeField] private UnityEvent onDefeated;
    [Tooltip("Player died mid-fight: reopen the doors, stop the music...")]
    [SerializeField] private UnityEvent onReset;

    /// <summary>Boss ids beaten this session. Hook into your save system later.</summary>
    public static readonly HashSet<string> DefeatedBosses = new HashSet<string>();

    public bool HasStarted { get; private set; }
    public bool IsDefeated { get; private set; }

    private Coroutine starting;

    private void Awake()
    {
        var trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
        if (bossBar != null) bossBar.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (boss == null) { Debug.LogError("[BossEncounter] No boss assigned.", this); enabled = false; return; }

        if (!string.IsNullOrEmpty(bossId) && DefeatedBosses.Contains(bossId))
        {
            IsDefeated = true;
            boss.gameObject.SetActive(false);
            onDefeated.Invoke();            // doors already open
            return;
        }

        boss.SetDormant(true);
    }

    private void OnEnable()
    {
        PlayerHealth.PlayerDied += OnPlayerDied;
        if (boss != null) boss.Died += OnBossDied;
    }

    private void OnDisable()
    {
        PlayerHealth.PlayerDied -= OnPlayerDied;
        if (boss != null) boss.Died -= OnBossDied;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (HasStarted || IsDefeated || boss == null) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        starting = StartCoroutine(BeginRoutine());
    }

    private IEnumerator BeginRoutine()
    {
        HasStarted = true;
        onStarted.Invoke();

        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        if (bossBar != null)
        {
            bossBar.Bind(boss.Health, boss.DisplayName);
            bossBar.gameObject.SetActive(true);
        }

        boss.SetDormant(false);
        boss.Alert();
        starting = null;
    }

    private void OnBossDied(Enemy _)
    {
        IsDefeated = true;
        if (!string.IsNullOrEmpty(bossId)) DefeatedBosses.Add(bossId);
        if (bossBar != null) bossBar.gameObject.SetActive(false);
        onDefeated.Invoke();
    }

    private void OnPlayerDied()
    {
        if (!resetWhenPlayerDies || !HasStarted || IsDefeated || boss == null || boss.IsDead) return;

        if (starting != null) { StopCoroutine(starting); starting = null; }
        boss.ResetToSpawn();
        boss.SetDormant(true);
        if (bossBar != null) bossBar.gameObject.SetActive(false);

        HasStarted = false;        // walking in again restarts it
        onReset.Invoke();
    }
}

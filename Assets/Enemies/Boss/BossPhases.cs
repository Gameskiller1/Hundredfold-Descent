using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turns an Enemy into a multi-phase boss. Add next to Enemy + PatrolChaseBrain (or your own brain).
/// A phase starts when health drops to its threshold. Each phase can change speed, damage,
/// which attacks are allowed, play a transition (roar, invulnerable) and fire UnityEvents
/// (spawn adds, change music, break the floor...).
/// Phase 1 should start at 1 (100%). Order phases from highest to lowest threshold.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class BossPhases : MonoBehaviour
{
    [Serializable]
    public class Phase
    {
        public string name = "Phase";
        [Tooltip("Starts when health is at or below this fraction (1 = from the start, 0.5 = half health).")]
        [Range(0f, 1f)] public float startsAtHealth = 1f;

        [Header("Changes")]
        [Min(0f)] public float speedMultiplier = 1f;
        [Min(0f)] public float damageMultiplier = 1f;
        [Tooltip("Attacks allowed in this phase. Empty = leave the attacks as they are.")]
        public EnemyAttack[] attacks;

        [Header("Transition")]
        [Tooltip("Seconds it freezes and is invulnerable while changing phase (roar). 0 = instant.")]
        [Min(0f)] public float transitionTime = 0f;
        [Tooltip("Animator state to play during the transition. Optional.")]
        public string transitionAnimation;
        [Tooltip("Heals this fraction of max health when the phase starts.")]
        [Range(0f, 1f)] public float healOnEnter = 0f;

        public UnityEvent onEnter;
    }

    [SerializeField] private Phase[] phases = new Phase[] { new Phase { name = "Phase 1" } };

    public int CurrentIndex { get; private set; } = -1;
    public Phase Current => CurrentIndex >= 0 && CurrentIndex < phases.Length ? phases[CurrentIndex] : null;
    public bool IsTransitioning { get; private set; }
    public int PhaseCount => phases.Length;

    /// <summary>(index, phase) - camera shake, music, UI...</summary>
    public event Action<int, Phase> PhaseChanged;

    private Enemy enemy;
    private Health health;
    private Coroutine transition;
    private bool transitionLocks;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.Changed += OnHealthChanged;
        enemy.ReturnedToSpawn += ResetPhases;
    }

    private void OnDisable()
    {
        health.Changed -= OnHealthChanged;
        enemy.ReturnedToSpawn -= ResetPhases;
        ReleaseTransition();
    }

    private void Start()
    {
        if (CurrentIndex < 0 && phases.Length > 0) Enter(0, instant: true);
    }

    private void OnHealthChanged(float current, float max)
    {
        if (!health.IsAlive || phases.Length == 0) return;

        // A big hit can skip phases: jump to the last one whose threshold we're under
        int target = CurrentIndex;
        for (int i = Mathf.Max(0, CurrentIndex + 1); i < phases.Length; i++)
            if (health.Normalized <= phases[i].startsAtHealth) target = i;

        if (target > CurrentIndex) Enter(target, instant: false);
    }

    private void Enter(int index, bool instant)
    {
        ReleaseTransition();
        CurrentIndex = index;
        Phase p = phases[index];

        enemy.SpeedMultiplier = p.speedMultiplier;
        enemy.DamageMultiplier = p.damageMultiplier;

        if (p.attacks != null && p.attacks.Length > 0)
            foreach (var a in enemy.Attacks)
                if (a != null) a.enabled = Array.IndexOf(p.attacks, a) >= 0;

        if (p.healOnEnter > 0f) health.Heal(health.Max * p.healOnEnter);

        if (instant) { p.onEnter.Invoke(); return; }

        PhaseChanged?.Invoke(index, p);
        p.onEnter.Invoke();
        if (p.transitionTime > 0f) transition = StartCoroutine(TransitionRoutine(p));
    }

    private IEnumerator TransitionRoutine(Phase p)
    {
        IsTransitioning = true;
        transitionLocks = true;

        enemy.CancelAttacks();
        enemy.StopHorizontal();
        enemy.LockAI();
        enemy.LockMovement();
        health.LockInvulnerable();
        enemy.Anim?.Play(p.transitionAnimation, p.transitionTime);

        yield return new WaitForSeconds(p.transitionTime);

        transition = null;
        ReleaseTransition();
    }

    private void ReleaseTransition()
    {
        if (transition != null) { StopCoroutine(transition); transition = null; }
        if (transitionLocks)
        {
            transitionLocks = false;
            enemy.UnlockAI();
            enemy.UnlockMovement();
            health.UnlockInvulnerable();
        }
        IsTransitioning = false;
    }

    /// <summary>Back to phase 1 (boss reset after the player dies).</summary>
    public void ResetPhases()
    {
        ReleaseTransition();
        CurrentIndex = -1;
        foreach (var a in enemy.Attacks) if (a != null) a.enabled = true;
        if (phases.Length > 0) Enter(0, instant: true);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Inherit from this to make a new ability. Drop it on the Player, assign an input action, done.
/// </summary>
public abstract class PlayerAbility : MonoBehaviour
{
    [Header("Ability")]
    [SerializeField] protected InputActionReference inputAction; // button that triggers it
    [SerializeField] protected float cooldown = 0f;            // seconds between uses

    protected PlayerController player;      // owner, found automatically
    protected float lastUseTime = -999f;    // very negative = ready at start

    public bool IsOnCooldown => Time.time < lastUseTime + cooldown;
    public float CooldownRemaining => Mathf.Max(0f, lastUseTime + cooldown - Time.time); // for UI

    protected virtual void Awake()
    {
        // Works whether the ability is on the player or on a child object
        player = GetComponentInParent<PlayerController>();
    }

    protected virtual void OnEnable()
    {
        if (inputAction == null) return;
        inputAction.action.Enable();
        inputAction.action.performed += HandleInput; // listen for button press
    }

    protected virtual void OnDisable()
    {
        if (inputAction == null) return;
        inputAction.action.performed -= HandleInput; // always unsubscribe to avoid leaks
    }

    // Input callback -> same path as code activation
    private void HandleInput(InputAction.CallbackContext ctx) => TryActivate();

    /// <summary>(ability, reason) - why a press did nothing. Debug console listens.</summary>
    public static event System.Action<PlayerAbility, string> Blocked;
    /// <summary>An ability actually started.</summary>
    public static event System.Action<PlayerAbility> Activated;

    /// <summary>Can also be called from code (AI, UI buttons, combos...)</summary>
    public bool TryActivate()
    {
        // All shared checks in one place
        string why = null;
        if (!enabled) why = "ability is disabled";
        else if (player == null) why = "no PlayerController found";
        else if (!player.CanAct) why = "player can't act (actions locked)";
        else if (IsOnCooldown) why = $"on cooldown ({CooldownRemaining:0.00}s left)";
        else if (!CanActivate()) why = "CanActivate() returned false";
        if (why != null) { Blocked?.Invoke(this, why); return false; }

        lastUseTime = Time.time; // start cooldown
        Activated?.Invoke(this);
        Activate();
        return true;
    }

    /// <summary>Extra conditions (mana, stamina, grounded...). Override if needed.</summary>
    protected virtual bool CanActivate() => true;

    /// <summary>What the ability does.</summary>
    protected abstract void Activate();


}
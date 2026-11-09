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

    /// <summary>Can also be called from code (AI, UI buttons, combos...)</summary>
    public bool TryActivate()
    {
        // All shared checks in one place
        if (!enabled || player == null || !player.CanAct || IsOnCooldown || !CanActivate())
            return false;

        lastUseTime = Time.time; // start cooldown
        Activate();
        return true;
    }

    /// <summary>Extra conditions (mana, stamina, grounded...). Override if needed.</summary>
    protected virtual bool CanActivate() => true;

    /// <summary>What the ability does.</summary>
    protected abstract void Activate();
}
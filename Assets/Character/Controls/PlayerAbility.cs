using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Inherit from this to make a new ability. Drop it on the Player, assign an input action, done.
/// </summary>
public abstract class PlayerAbility : MonoBehaviour
{
    [Header("Ability")]
    [SerializeField] protected InputActionReference inputAction;
    [SerializeField] protected float cooldown = 0.5f;

    protected PlayerController player;
    protected float lastUseTime = -999f;

    public bool IsOnCooldown => Time.time < lastUseTime + cooldown;
    public float CooldownRemaining => Mathf.Max(0f, lastUseTime + cooldown - Time.time);

    protected virtual void Awake()
    {
        player = GetComponentInParent<PlayerController>();
    }

    protected virtual void OnEnable()
    {
        if (inputAction == null) return;
        inputAction.action.Enable();
        inputAction.action.performed += HandleInput;
    }

    protected virtual void OnDisable()
    {
        if (inputAction == null) return;
        inputAction.action.performed -= HandleInput;
    }

    private void HandleInput(InputAction.CallbackContext ctx) => TryActivate();

    /// <summary>Can also be called from code (AI, UI buttons, combos...)</summary>
    public bool TryActivate()
    {
        if (!enabled || player == null || !player.CanAct || IsOnCooldown || !CanActivate())
            return false;

        lastUseTime = Time.time;
        Activate();
        return true;
    }

    /// <summary>Extra conditions (mana, stamina, grounded...). Override if needed.</summary>
    protected virtual bool CanActivate() => true;

    /// <summary>What the ability does.</summary>
    protected abstract void Activate();
}
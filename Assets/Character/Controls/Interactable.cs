using UnityEngine;

/// <summary>
/// Base for anything the player can interact with (torches, chests, levers, NPCs...).
/// Needs a Collider2D (Is Trigger) on the "Interactable" layer so InteractAbility can find it.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Header("Interactable")]
    [Tooltip("Text for a future UI prompt, e.g. 'Light', 'Open'.")]
    [SerializeField] private string promptText = "Interact";

    public string PromptText => promptText;

    /// <summary>Extra conditions (already used, needs key...). Override if needed.</summary>
    public virtual bool CanInteract(PlayerController player) => true;

    /// <summary>What happens on interaction.</summary>
    public abstract void Interact(PlayerController player);
}
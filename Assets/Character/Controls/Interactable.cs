using TMPro;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interactable")]
    [SerializeField] private string promptText = "Interact";
    [SerializeField, Min(0f)] private float holdTime = 0f;

    [Header("Prompt")]
    [SerializeField] private TMP_Text promptLabel;

    public string PromptText => promptText;
    public virtual float HoldTime => holdTime;

    public virtual bool CanInteract(PlayerController player) => true;
    public abstract void Interact(PlayerController player);

    public virtual string GetPromptText() =>
        HoldTime > 0f ? $"Hold E {HoldTime:0.#}s: {promptText}" : $"E: {promptText}";

    // Return false to hide the label completely
    public virtual bool ShouldShowPrompt(PlayerController player, InteractAbility ability) => true;

    public void SetPrompt(bool visible, string text = null)
    {
        if (promptLabel == null) return;
        if (visible && text != null) promptLabel.text = text;
        if (promptLabel.gameObject.activeSelf != visible)
            promptLabel.gameObject.SetActive(visible);
    }

    public TMP_Text GetLabel() => promptLabel;
}
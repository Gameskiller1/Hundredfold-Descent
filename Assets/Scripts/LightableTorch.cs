using UnityEngine;

/// <summary>
/// Torch/brazier/candle: interact to enable its light (and flame sprite, particles...).
/// </summary>
public class LightableTorch : Interactable
{
    [Header("Torch")]
    [Tooltip("Children to enable when lit (Light 2D, flame sprite, particles...).")]
    [SerializeField] private GameObject[] objectsToEnable;
    [SerializeField] private bool startLit = false;    // pre-lit torches in a level
    [SerializeField] private bool canExtinguish = false; // true = pressing E again turns it off

    public bool IsLit { get; private set; }

    private void Awake() => SetLit(startLit); // sync visuals with the starting state

    // Already lit and can't be put out? Then nothing to do
    public override bool CanInteract(PlayerController player) => !IsLit || canExtinguish;

    public override void Interact(PlayerController player) => SetLit(!IsLit);

    /// <summary>Public so other things can light it too (fire spells, events, save system).</summary>
    public void SetLit(bool lit)
    {
        IsLit = lit;
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(lit);
    }
}
using System.Collections.Generic;
using UnityEngine;

public class LightableTorch : Interactable
{
    [Header("Torch")]
    [SerializeField] private GameObject[] objectsToEnable;
    [SerializeField] private bool startLit = false;
    [SerializeField] private bool canExtinguish = false;

    [Header("Prompt color")]
    [Tooltip("Color of the Attach prompt. Set to match requiredLightId.")]
    [SerializeField] private Color promptColor = new Color(1f, 0.4f, 0.3f);
    public Color PromptColor => promptColor;

    [Header("Light requirement (ID only)")]
    [SerializeField] private string requiredLightId = "red";
    [SerializeField, Min(0f)] private float lightReward = 15f;

    [Header("Flame tint")]
    [SerializeField] private List<GameObject> tintTargets = new List<GameObject>();
    [SerializeField] private bool tintFlame = true;

    public bool IsLit { get; private set; }
    public string RequiredLightId => requiredLightId;

    private void Awake()
    {
        SetLit(startLit);
        GameLog.Write(LogLevel.Info, LogCat.Interact,
            $"{name} torch tracked: {(IsLit ? "LIT" : "unlit")}, needs '{requiredLightId}' light");
    }

    public override bool CanInteract(PlayerController player) => !IsLit || canExtinguish;

    // Hide unless player carries the matching orb. No light = no text.
    public override bool ShouldShowPrompt(PlayerController player, InteractAbility ability)
    {
        if (IsLit) return false;
        if (ability == null || ability.Carried == null) return false;
        return LightIds.Matches(requiredLightId, ability.Carried.LightId);
    }

    public override string GetPromptText() => "E: Attach light";

    public override void Interact(PlayerController player)
    {
        if (IsLit)
        {
            SetLit(false);
            GameLog.Write(LogLevel.Info, LogCat.Interact, $"{name} extinguished");
            return;
        }
        GameLog.Write(LogLevel.Warning, LogCat.Interact, $"{name} needs '{requiredLightId}' light.");
    }

    public bool TryIgnite(LightOrb orb)
    {
        if (IsLit) return false;
        if (!LightIds.Matches(requiredLightId, orb.LightId))
        {
            GameLog.Write(LogLevel.Warning, LogCat.Interact,
                $"{name} rejected '{orb.LightId}' (needs '{requiredLightId}')");
            return false;
        }
        SetLit(true, orb.LightColor);
        var light = PlayerController.Instance != null ? PlayerController.Instance.GetComponent<PlayerLight>() : null;
        if (light != null) light.Add(lightReward);
        GameLog.Write(LogLevel.Success, LogCat.Interact, $"{name} LIT by {orb.LightId} (+{lightReward:0.#} Light)");
        return true;
    }

    public void SetLit(bool lit) => SetLit(lit, null);
    private void SetLit(bool lit, Color? tint = null)
    {
        IsLit = lit;
        foreach (var obj in objectsToEnable)
            if (obj != null) obj.SetActive(lit);
        if (lit && tintFlame && tint.HasValue)
            foreach (var t in tintTargets)
                if (t != null) LightOrb.TintObject(t, tint.Value);
    }
}
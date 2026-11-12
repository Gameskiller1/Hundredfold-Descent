
using UnityEngine;

/// <summary>
/// The spell as an ITEM. Create via: Assets > Create > Game > Spell.
/// Never modify at runtime (changes in Play Mode are saved to the asset!).
/// </summary>
[CreateAssetMenu(menuName = "Game/Spell", fileName = "Spell_")]
public class SpellData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string displayName = "New Spell";
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(2, 5)] private string description;

    [Header("Stats")]
    [Tooltip("Light spent when the spell actually goes off.")]
    [SerializeField, Min(0f)] private float lightCost = 10f;
    [Tooltip("Seconds after a successful cast before it can be used again.")]
    [SerializeField, Min(0f)] private float cooldown = 1f;

    [Header("Behaviour")]
    [Tooltip("Prefab with a Spell component (e.g. LightningStrikeAbility). Spawned when equipped.")]
    [SerializeField] private Spell behaviourPrefab;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public float LightCost => lightCost;
    public float Cooldown => cooldown;
    public Spell BehaviourPrefab => behaviourPrefab;
}

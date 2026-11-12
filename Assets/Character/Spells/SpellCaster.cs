
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Goes on the player. Holds equip slots; each slot has its own key.
/// Equip(slot, data) spawns the spell's behaviour prefab as a child.
/// </summary>
[DisallowMultipleComponent]
public class SpellCaster : MonoBehaviour
{
    [Serializable]
    private class Slot
    {
        public InputActionReference input;     // Spell1 = Z, Spell2 = X...
        public SpellData startingSpell;        // optional starting loadout

        [NonSerialized] public SpellData data;
        [NonSerialized] public Spell instance;
        [NonSerialized] public Action<InputAction.CallbackContext> handler;
    }

    [SerializeField] private Slot[] slots = new Slot[2];

    [Header("Targeting Input (shared by all aimed spells)")]
    [SerializeField] private InputActionReference confirmAction;   // <Mouse>/leftButton
    [SerializeField] private InputActionReference cancelAction;    // <Keyboard>/q
    [SerializeField] private InputActionReference pointAction;     // <Mouse>/position

    [Header("Optional")]
    [Tooltip("Canvas with a dark fullscreen Image (Raycast Target OFF). Shown while aiming.")]
    [SerializeField] private GameObject dimOverlayPrefab;

    public PlayerController Player { get; private set; }
    public PlayerLight Light { get; private set; }
    public InputActionReference ConfirmAction => confirmAction;
    public InputActionReference CancelAction => cancelAction;
    public InputActionReference PointAction => pointAction;
    public int SlotCount => slots.Length;

    /// <summary>(slot index, new data or null) - for hotbar UI.</summary>
    public event Action<int, SpellData> SlotChanged;
    /// <summary>Any equipped spell was cast.</summary>
    public event Action<Spell> SpellCast;

    private GameObject dimInstance;

    private void Awake()
    {
        Player = GetComponentInParent<PlayerController>();
        Light = GetComponentInParent<PlayerLight>();
    }

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].startingSpell != null) Equip(i, slots[i].startingSpell);
    }

    private void OnEnable()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;   // capture for the lambda
            var s = slots[i];
            if (s.input == null) continue;
            s.handler = _ => Cast(index);
            s.input.action.performed += s.handler;
            s.input.action.Enable();
        }
    }

    private void OnDisable()
    {
        foreach (var s in slots)
            if (s.input != null && s.handler != null) s.input.action.performed -= s.handler;
    }

    // =====================  PUBLIC API  =====================

    /// <summary>Puts a spell in a slot (replaces what was there). null = empty the slot.</summary>
    public void Equip(int slot, SpellData data)
    {
        if (!ValidSlot(slot)) return;

        // Same spell already in another slot -> move it
        int existing = IndexOf(data);
        if (data != null && existing >= 0 && existing != slot) Unequip(existing);

        Unequip(slot);
        if (data == null) return;

        if (data.BehaviourPrefab == null)
        {
            Debug.LogError($"[SpellCaster] '{data.name}' has no Behaviour Prefab.", data);
            return;
        }

        var instance = Instantiate(data.BehaviourPrefab, transform);
        instance.name = $"[Slot {slot}] {data.DisplayName}";
        instance.Init(this, data);
        instance.Casted += OnSpellCast;

        slots[slot].data = data;
        slots[slot].instance = instance;
        SlotChanged?.Invoke(slot, data);
    }

    public void Unequip(int slot)
    {
        if (!ValidSlot(slot)) return;
        var s = slots[slot];
        if (s.instance == null && s.data == null) return;

        if (s.instance != null)
        {
            s.instance.Casted -= OnSpellCast;
            s.instance.ForceCancel();                 // unfreeze / unlock immediately
            s.instance.gameObject.SetActive(false);
            Destroy(s.instance.gameObject);
        }

        s.instance = null;
        s.data = null;
        SlotChanged?.Invoke(slot, null);
    }

    public bool Cast(int slot) => ValidSlot(slot) && slots[slot].instance != null && slots[slot].instance.TryCast();

    public SpellData GetData(int slot) => ValidSlot(slot) ? slots[slot].data : null;
    public Spell GetSpell(int slot) => ValidSlot(slot) ? slots[slot].instance : null;

    public int IndexOf(SpellData data)
    {
        if (data == null) return -1;
        for (int i = 0; i < slots.Length; i++) if (slots[i].data == data) return i;
        return -1;
    }

    public bool IsEquipped(SpellData data) => IndexOf(data) >= 0;

    /// <summary>Used by TargetedSpell.</summary>
    public void SetDim(bool on)
    {
        if (dimOverlayPrefab == null) return;
        if (dimInstance == null) dimInstance = Instantiate(dimOverlayPrefab, transform);
        dimInstance.SetActive(on);
    }

    // =====================  HELPERS  =====================

    private bool ValidSlot(int i)
    {
        if (i >= 0 && i < slots.Length) return true;
        Debug.LogWarning($"[SpellCaster] Slot {i} doesn't exist.", this);
        return false;
    }

    private void OnSpellCast(Spell s) => SpellCast?.Invoke(s);
}

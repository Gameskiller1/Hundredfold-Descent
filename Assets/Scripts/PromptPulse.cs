using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class PromptPulse : MonoBehaviour
{
    [SerializeField] private Vector3 baseScale;
    [SerializeField] private float bobAmount = 0.12f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private float pulseSpeed = 3f;

    private TMP_Text tmp;
    private Vector3 basePos;
    private bool hasBase;

    private void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;
        RefreshFromParent(); // auto-color on spawn
    }

    private void OnEnable()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale; // use whatever you set in Inspector
        hasBase = true;
        RefreshFromParent();
    }

    // Called by InteractAbility for the torch-carried case (overrides parent color)
    public void SetLightColor(Color c)
    {
        if (tmp == null) tmp = GetComponent<TMP_Text>();
        tmp.color = c;
    }

    // Parent color: orb -> its color, torch -> its promptColor
    public void RefreshFromParent()
    {
        if (tmp == null) tmp = GetComponent<TMP_Text>();
        var orb = GetComponentInParent<LightOrb>();
        if (orb != null) { tmp.color = orb.LightColor; return; }
        var torch = GetComponentInParent<LightableTorch>();
        if (torch != null) { tmp.color = torch.PromptColor; return; }
    }

    private void Update()
    {
        if (!hasBase) return;
        float t = Time.time;
        transform.localPosition = basePos + Vector3.up * Mathf.Sin(t * bobSpeed) * bobAmount;
        float s = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * s;
    }
}
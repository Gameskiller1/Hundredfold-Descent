using UnityEngine;

/// <summary>
/// Side-view camera: separate X/Y smoothing + look-ahead in facing direction.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave empty: it finds the Player automatically (works after scene changes).")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f); // slightly above player

    [Header("Follow")]
    [Tooltip("0 = locked. 0.1-0.2 = smooth.")]
    [SerializeField] private float smoothTimeX = 0.12f;  // horizontal follow
    [SerializeField] private float smoothTimeY = 0.25f;  // vertical follow (softer for jumps)

    [Header("Look Ahead")]
    [SerializeField] private float lookAhead = 1.5f;      // how far ahead of facing
    [SerializeField] private float lookAheadSpeed = 4f;   // how fast it shifts when turning

    private PlayerController player; // cached for FacingSign
    private float velX, velY;        // SmoothDamp velocities
    private float currentLookAhead;  // current shift

    private void Start()
    {
        FindTargetIfMissing();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) { FindTargetIfMissing(); return; }

        // Smoothly shift look-ahead towards facing direction
        if (player != null)
            currentLookAhead = Mathf.MoveTowards(currentLookAhead, player.FacingSign * lookAhead,
                                                 lookAheadSpeed * Time.deltaTime);

        Vector3 desired = GetDesiredPosition();

        // Each axis gets its own smoothing
        float x = smoothTimeX <= 0f ? desired.x
                : Mathf.SmoothDamp(transform.position.x, desired.x, ref velX, smoothTimeX);
        float y = smoothTimeY <= 0f ? desired.y
                : Mathf.SmoothDamp(transform.position.y, desired.y, ref velY, smoothTimeY);

        transform.position = new Vector3(x, y, transform.position.z);
    }

    /// <summary>Instantly center on the player (use after teleport / level load).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        velX = velY = 0f;
        currentLookAhead = player != null ? player.FacingSign * lookAhead : 0f;
        transform.position = GetDesiredPosition();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        player = target != null ? target.GetComponent<PlayerController>() : null;
        SnapToTarget();
    }

    // Target position + offset + look-ahead, keep camera Z
    private Vector3 GetDesiredPosition()
    {
        return new Vector3(target.position.x + offset.x + currentLookAhead,
                           target.position.y + offset.y,
                           transform.position.z);
    }

    // Cheap: uses the static singleton instead of searching the scene
    private void FindTargetIfMissing()
    {
        if (target != null) return;
        player = PlayerController.Instance;
        if (player != null) target = player.transform;
    }
}
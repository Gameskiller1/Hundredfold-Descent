using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Leave empty: it finds the Player automatically (works after scene changes).")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Header("Follow")]
    [Tooltip("0 = locked on the player. 0.1-0.2 = smooth.")]
    [SerializeField] private float smoothTime = 0.12f;

    private Vector3 velocity;

    private void Start()
    {
        FindTargetIfMissing();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) { FindTargetIfMissing(); return; }

        Vector3 desired = GetDesiredPosition();

        transform.position = smoothTime <= 0f
            ? desired
            : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }

    /// <summary>Instantly center on the player (use after teleport / level load).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = GetDesiredPosition();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    private Vector3 GetDesiredPosition()
    {
        return new Vector3(target.position.x + offset.x,
                           target.position.y + offset.y,
                           transform.position.z);
    }

    private void FindTargetIfMissing()
    {
        if (target != null) return;
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) target = player.transform;
    }
}
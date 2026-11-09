using System.Collections;
using UnityEngine;

public class DashAbility : PlayerAbility
{
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 14f;
    [SerializeField] private float dashDuration = 0.12f;

    public bool IsDashing { get; private set; }

    protected override bool CanActivate() => !IsDashing;

    protected override void Activate() => StartCoroutine(DashRoutine());

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        player.LockMovement();

        Vector2 dir = player.MoveInput.sqrMagnitude > 0.01f ? player.MoveInput.normalized : player.FacingDirection;
        print(player.MoveInput);

        player.Rb.linearVelocity = dir * dashSpeed;

        yield return new WaitForSeconds(dashDuration);

        player.StopImmediately();
        player.UnlockMovement();
        IsDashing = false;
    }
}
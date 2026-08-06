using Unity.Netcode;
using UnityEngine;

namespace Office.Gameplay
{
    /// <summary>
    /// Owner-side bridge from movement state to Animator parameters. Remote
    /// instances receive the parameters through the OwnerNetworkAnimator, so
    /// this component only writes on the owning client.
    /// </summary>
    public sealed class PlayerAnimationDriver : NetworkBehaviour
    {
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int CrouchHash = Animator.StringToHash("Crouch");

        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Animator animator;

        [Tooltip("Seconds of smoothing applied to the locomotion floats.")]
        [SerializeField] private float dampTime = 0.1f;

        private void Update()
        {
            if (!IsSpawned || !IsOwner || movement == null || animator == null) return;

            var local = transform.InverseTransformDirection(movement.PlanarVelocity);
            var maxSpeed = Mathf.Max(0.01f, movement.MaxPlanarSpeed);
            var deltaTime = Time.deltaTime;

            animator.SetFloat(MoveXHash, local.x / maxSpeed, dampTime, deltaTime);
            animator.SetFloat(MoveYHash, local.z / maxSpeed, dampTime, deltaTime);
            animator.SetBool(GroundedHash, movement.IsGrounded);
            animator.SetFloat(CrouchHash, movement.CrouchBlend);
        }
    }
}

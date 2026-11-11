namespace DeadCounty.Animation
{
    // What ProceduralHumanoidAnimator reads from whatever drives the body (player controller, zombie agent).
    // The body is assumed to move along its own transform.forward.
    public interface IHumanoidMotion
    {
        // Horizontal speed actually covered (m/s), after collisions.
        float CurrentSpeed { get; }
        bool IsGrounded { get; }
        bool IsCrouching { get; }
        // Where the head should look, in world space (deg).
        float AimYaw { get; }
        float AimPitch { get; }
        PoseModifiers Pose { get; }
        // Changes on every teleport or respawn so the animator snaps instead of reading the warp as motion.
        int MotionResetId { get; }
    }
}

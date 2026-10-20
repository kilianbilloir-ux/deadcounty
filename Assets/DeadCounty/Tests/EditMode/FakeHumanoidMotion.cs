using DeadCounty.Animation;

namespace DeadCounty.Tests
{
    // Scripted motion source for animator tests.
    sealed class FakeHumanoidMotion : IHumanoidMotion
    {
        public float CurrentSpeed { get; set; }
        public bool IsGrounded { get; set; } = true;
        public bool IsCrouching { get; set; }
        public float AimYaw { get; set; }
        public float AimPitch { get; set; }
        public PoseModifiers Pose { get; set; } = PoseModifiers.Neutral;
        public int MotionResetId { get; set; }
    }
}

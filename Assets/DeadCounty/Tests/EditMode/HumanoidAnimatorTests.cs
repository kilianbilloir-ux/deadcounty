using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    public class HumanoidAnimatorTests
    {
        const float Dt = HumanoidTestRig.Dt;

        static HumanoidTestRig Survivor() => new(ScriptableObject.CreateInstance<GaitProfile>());

        static HumanoidTestRig Shambler(bool limping, bool leftIsBad)
        {
            var rig = new HumanoidTestRig(GaitProfile.CreateShambler());
            var limp = LimpSettings.Default;
            limp.Enabled = limping;
            limp.LeftIsBad = leftIsBad;
            rig.Animator.Limp = limp;
            return rig;
        }

        // Fastest horizontal ankle motion over frames where the foot was planted in this and the previous
        // frame, after a 1 s settling time, over 3 s at constant speed. `flightFrames` counts the frames with
        // both feet off the ground.
        static float MaxStanceSlip(HumanoidTestRig rig, float speed, out int stanceFrames, out int flightFrames, float yawRate = 0f, float maxRollDeg = float.PositiveInfinity)
        {
            rig.Initialize();
            var max = 0f;
            var frames = 0;
            var flight = 0;
            var lastLeft = rig.LeftAnkle.position;
            var lastRight = rig.RightAnkle.position;
            bool wasLeftPlanted = false, wasRightPlanted = false;
            float lastLeftRoll = 0f, lastRightRoll = 0f;
            for (var frame = 0; frame < 180; frame++)
            {
                rig.Step(speed, yawRate);
                var left = rig.LeftAnkle.position;
                var right = rig.RightAnkle.position;
                if (frame >= 60)
                {
                    if (!rig.Animator.LeftFootPlanted && !rig.Animator.RightFootPlanted) flight++;
                    if (wasLeftPlanted && rig.Animator.LeftFootPlanted
                        && Mathf.Abs(rig.Animator.LeftFootRoll) <= maxRollDeg && Mathf.Abs(lastLeftRoll) <= maxRollDeg)
                        Measure(left - lastLeft);
                    if (wasRightPlanted && rig.Animator.RightFootPlanted
                        && Mathf.Abs(rig.Animator.RightFootRoll) <= maxRollDeg && Mathf.Abs(lastRightRoll) <= maxRollDeg)
                        Measure(right - lastRight);
                }
                lastLeft = left;
                lastRight = right;
                wasLeftPlanted = rig.Animator.LeftFootPlanted;
                wasRightPlanted = rig.Animator.RightFootPlanted;
                lastLeftRoll = rig.Animator.LeftFootRoll;
                lastRightRoll = rig.Animator.RightFootRoll;
            }
            stanceFrames = frames;
            flightFrames = flight;
            return max;

            void Measure(Vector3 delta)
            {
                max = Mathf.Max(max, new Vector2(delta.x, delta.z).magnitude / Dt);
                frames++;
            }
        }

        [TestCase(1.5f)]
        [TestCase(3.5f)]
        [TestCase(6f)]
        public void SurvivorStanceFootStaysPlanted(float speed)
        {
            using var rig = Survivor();
            var slip = MaxStanceSlip(rig, speed, out var frames, out _);
            TestContext.WriteLine($"max slip {slip:0.00000} m/s over {frames} stance frames");
            Assert.That(frames, Is.GreaterThan(30));
            Assert.That(slip, Is.LessThanOrEqualTo(0.01f));
        }

        [TestCase(0.55f, true)]
        [TestCase(0.55f, false)]
        [TestCase(1.2f, true)]
        [TestCase(1.2f, false)]
        public void LimpingShamblerStanceFootStaysPlanted(float speed, bool leftIsBad)
        {
            using var rig = Shambler(true, leftIsBad);
            var slip = MaxStanceSlip(rig, speed, out var frames, out _);
            TestContext.WriteLine($"max slip {slip:0.00000} m/s over {frames} stance frames");
            Assert.That(frames, Is.GreaterThan(30));
            Assert.That(slip, Is.LessThanOrEqualTo(0.02f));
        }

        // Turning on the spot slides each foothold back by the frame's yaw: no bank, exact.
        [TestCase(120f)]
        [TestCase(-120f)]
        public void TurnInPlaceKeepsPlantedFeetInPlace(float yawRate)
        {
            using var rig = Survivor();
            var slip = MaxStanceSlip(rig, 0f, out var frames, out _, yawRate);
            TestContext.WriteLine($"turn-in-place slip {slip:0.00000} m/s over {frames} stance frames (yaw {yawRate})");
            Assert.That(frames, Is.GreaterThan(30));
            Assert.That(slip, Is.LessThanOrEqualTo(0.003f));
        }

        // Turning while walking banks the body, so the rising ankle of a toeing-off boot reads as slip:
        // only measure while the boot is flat (|roll| <= 3 deg needs moving ~= 1). The toe pivoting about
        // its contact (~0.16 m/s at 60 deg/s) is out of scope. N > 1 LOD is untested on purpose: with the
        // rotate-then-move integration the rig and the game agree frame by frame, but an accumulated
        // multi-frame step has no exact single-frame correction (second order in yaw x travel).
        // Entering a turn banks the body within a few frames; planted feet must ride it, not jump.
        // Flat-foot gated like WalkWhileTurning: the re-step itself moves ankles, only the excess counts.
        // Measured 0.05 with the transport; the un-transported bank drag alone adds ~0.16.
        [TestCase(120f)]
        [TestCase(-120f)]
        public void TurnEntryKeepsFeetBounded(float yawRate)
        {
            using var rig = Survivor();
            rig.Initialize();
            for (var frame = 0; frame < 60; frame++) rig.Step(1.5f);
            var max = 0f;
            var frames = 0;
            var lastLeft = rig.LeftAnkle.position;
            var lastRight = rig.RightAnkle.position;
            var wasLeftPlanted = rig.Animator.LeftFootPlanted;
            var wasRightPlanted = rig.Animator.RightFootPlanted;
            for (var frame = 0; frame < 15; frame++)
            {
                rig.Step(1.5f, yawRate);
                var left = rig.LeftAnkle.position;
                var right = rig.RightAnkle.position;
                if (wasLeftPlanted && rig.Animator.LeftFootPlanted && Mathf.Abs(rig.Animator.LeftFootRoll) <= 3f) Track(left - lastLeft);
                if (wasRightPlanted && rig.Animator.RightFootPlanted && Mathf.Abs(rig.Animator.RightFootRoll) <= 3f) Track(right - lastRight);
                lastLeft = left;
                lastRight = right;
                wasLeftPlanted = rig.Animator.LeftFootPlanted;
                wasRightPlanted = rig.Animator.RightFootPlanted;
            }
            TestContext.WriteLine($"turn-entry slip {max:0.00000} m/s over {frames} flat stance frames (yaw {yawRate})");
            Assert.That(frames, Is.GreaterThan(5));
            Assert.That(max, Is.LessThanOrEqualTo(0.10f), $"turn-entry slip {max:0.000} m/s over {frames} frames");

            void Track(Vector3 delta)
            {
                max = Mathf.Max(max, new Vector2(delta.x, delta.z).magnitude / Dt);
                frames++;
            }
        }
        [TestCase(60f)]
        [TestCase(-60f)]
        public void WalkWhileTurningKeepsFlatFootPlanted(float yawRate)
        {
            using var rig = Survivor();
            var slip = MaxStanceSlip(rig, 1.5f, out var frames, out _, yawRate, 3f);
            TestContext.WriteLine($"walking-turn slip {slip:0.00000} m/s over {frames} flat stance frames (yaw {yawRate})");
            Assert.That(frames, Is.GreaterThan(30));
            Assert.That(slip, Is.LessThanOrEqualTo(0.02f));
        }

        // The attack lunge covers 0.6 m in 0.2 s (3 m/s): the shambler still walks, with no flight phase.
        [TestCase(true)]
        [TestCase(false)]
        public void LungeHasNoFlightPhase(bool limping)
        {
            using var rig = Shambler(limping, false);
            rig.Motion.Pose = new PoseModifiers { ArmsForward = 1f, LookWeight = 1f, Lunge = 1f };
            MaxStanceSlip(rig, 3f, out _, out var flight);
            Assert.That(flight, Is.Zero);
        }

        // The attack as the agent drives it: standing through the wind-up, 0.6 m in 0.2 s, then standing again.
        // The stride under a planted foot jumps with the speed; the foot must not jump with it.
        [TestCase(true)]
        [TestCase(false)]
        public void LungeFromStandstillKeepsPlantedFeetInPlace(bool limping)
        {
            using var rig = Shambler(limping, false);
            rig.Initialize();
            rig.Motion.Pose = new PoseModifiers { ArmsForward = 1f, LookWeight = 1f };
            for (var frame = 0; frame < 60; frame++) rig.Step(0f);
            var maxSlip = 0f;
            var maxSwing = 0f;
            var lastLeft = rig.LeftAnkle.position;
            var lastRight = rig.RightAnkle.position;
            bool wasLeftPlanted = rig.Animator.LeftFootPlanted, wasRightPlanted = rig.Animator.RightFootPlanted;
            for (var frame = 0; frame < 72; frame++)
            {
                rig.Motion.Pose = new PoseModifiers { ArmsForward = 1f, LookWeight = 1f, Lunge = frame < 12 ? 1f : 0f };
                rig.Step(frame < 12 ? 3f : 0f);
                var left = rig.LeftAnkle.position;
                var right = rig.RightAnkle.position;
                Track(left - lastLeft, wasLeftPlanted && rig.Animator.LeftFootPlanted);
                Track(right - lastRight, wasRightPlanted && rig.Animator.RightFootPlanted);
                lastLeft = left;
                lastRight = right;
                wasLeftPlanted = rig.Animator.LeftFootPlanted;
                wasRightPlanted = rig.Animator.RightFootPlanted;
            }
            TestContext.WriteLine($"max planted slip {maxSlip:0.0000} m/s, fastest foot {maxSwing:0.00} m/s");
            Assert.That(maxSlip, Is.LessThanOrEqualTo(0.05f));
            // The step that follows the lunge peaks near 11 m/s; a foot jumping to a resized stride moved 0.4 m
            // and more in one frame (25+ m/s).
            Assert.That(maxSwing, Is.LessThanOrEqualTo(15f));

            void Track(Vector3 delta, bool planted)
            {
                var speed = Planar(delta) / Dt;
                if (planted) maxSlip = Mathf.Max(maxSlip, speed);
                maxSwing = Mathf.Max(maxSwing, speed);
            }

            static float Planar(Vector3 delta) => new Vector2(delta.x, delta.z).magnitude;
        }

        [Test]
        public void ArmsForwardRaisesShouldersToMinus100()
        {
            using var rig = Shambler(false, false);
            rig.Initialize();
            rig.Motion.Pose = new PoseModifiers { ArmsForward = 1f, LookWeight = 1f };
            for (var frame = 0; frame < 120; frame++) rig.Step(1.2f);
            foreach (var shoulder in new[] { rig.LeftShoulder, rig.RightShoulder })
            {
                var arm = shoulder.localRotation * Vector3.down;
                var pitch = Vector3.SignedAngle(Vector3.down, Vector3.ProjectOnPlane(arm, Vector3.right), Vector3.right);
                Assert.That(pitch, Is.EqualTo(-100f).Within(1f));
            }
        }

        [TestCase(false, 0f)]
        [TestCase(true, 85f)]
        public void HeadPastItsLimitRecentersOrHolds(bool shambler, float expectedYaw)
        {
            using var rig = shambler ? Shambler(false, false) : Survivor();
            rig.Initialize();
            rig.Motion.AimYaw = 150f;
            for (var frame = 0; frame < 120; frame++) rig.Step(0f);
            var look = Vector3.ProjectOnPlane(rig.Neck.forward, Vector3.up);
            Assert.That(Vector3.SignedAngle(rig.Root.forward, look, Vector3.up), Is.EqualTo(expectedYaw).Within(2f));
        }

        // Without a snap, a warp reads as a sprint: the lean clamps and the spine pitches hard.
        [Test]
        public void TeleportWithoutSnapSpikesLean()
        {
            using var rig = Survivor();
            rig.Initialize();
            rig.Motion.AimYaw = rig.Root.eulerAngles.y;
            for (var frame = 0; frame < 60; frame++) rig.Step(1.5f);
            rig.Root.position += rig.Root.forward * 2.5f;
            for (var frame = 0; frame < 3; frame++)
            {
                rig.Motion.AimYaw = rig.Root.eulerAngles.y;
                rig.Step(1.5f);
            }
            var tilt = Vector3.Angle(Vector3.up, rig.Spine.localRotation * Vector3.up);
            TestContext.WriteLine($"un-snapped warp lean {tilt:0.0} deg");
            Assert.That(tilt, Is.GreaterThan(6f), "a warp with no snap should spike the lean");
        }

        // Without the snap, the warp reads as a 1800 m/s surge and a 5400 deg/s turn: full lean and bank.
        [Test]
        public void TeleportCausesNoLeanOrBankSpike()
        {
            using var rig = Survivor();
            rig.Initialize();
            for (var frame = 0; frame < 60; frame++)
            {
                rig.Motion.AimYaw = rig.Root.eulerAngles.y;
                rig.Step(1.5f);
            }
            var lean = Tilt(rig.Spine);
            rig.Root.SetPositionAndRotation(rig.Root.position + new Vector3(30f, 0f, -12f), Quaternion.Euler(0f, 90f, 0f));
            rig.Motion.MotionResetId++;
            for (var frame = 0; frame < 30; frame++)
            {
                rig.Motion.AimYaw = rig.Root.eulerAngles.y;
                rig.Step(1.5f);
                Assert.That(Tilt(rig.Spine), Is.EqualTo(lean).Within(0.5f), $"lean at frame {frame}");
                Assert.That(Quaternion.Angle(rig.Model.localRotation, Quaternion.identity), Is.LessThan(0.5f), $"bank at frame {frame}");
            }

            static float Tilt(Transform joint) => Vector3.Angle(Vector3.up, joint.localRotation * Vector3.up);
        }
    }
}

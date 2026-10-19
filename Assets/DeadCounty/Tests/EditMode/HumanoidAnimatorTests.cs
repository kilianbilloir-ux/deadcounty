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
        static float MaxStanceSlip(HumanoidTestRig rig, float speed, out int stanceFrames, out int flightFrames)
        {
            rig.Initialize();
            var max = 0f;
            var frames = 0;
            var flight = 0;
            var lastLeft = rig.LeftAnkle.position;
            var lastRight = rig.RightAnkle.position;
            bool wasLeftPlanted = false, wasRightPlanted = false;
            for (var frame = 0; frame < 180; frame++)
            {
                rig.Step(speed);
                var left = rig.LeftAnkle.position;
                var right = rig.RightAnkle.position;
                if (frame >= 60)
                {
                    if (!rig.Animator.LeftFootPlanted && !rig.Animator.RightFootPlanted) flight++;
                    if (wasLeftPlanted && rig.Animator.LeftFootPlanted) Measure(left - lastLeft);
                    if (wasRightPlanted && rig.Animator.RightFootPlanted) Measure(right - lastRight);
                }
                lastLeft = left;
                lastRight = right;
                wasLeftPlanted = rig.Animator.LeftFootPlanted;
                wasRightPlanted = rig.Animator.RightFootPlanted;
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

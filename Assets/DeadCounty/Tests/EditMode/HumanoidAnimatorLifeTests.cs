using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // What makes a body look alive beyond its gait: the idle weight shift, a zombie's lurch and uneven arms, a
    // hand reaching out.
    public class HumanoidAnimatorLifeTests
    {
        const float Dt = HumanoidTestRig.Dt;

        static float ShoulderPitch(Transform shoulder)
        {
            var arm = shoulder.localRotation * Vector3.down;
            return Vector3.SignedAngle(Vector3.down, Vector3.ProjectOnPlane(arm, Vector3.right), Vector3.right);
        }

        [Test]
        public void StandingStillTheWeightCrossesFromOneLegToTheOtherOverPlantedFeet()
        {
            var gait = ScriptableObject.CreateInstance<GaitProfile>();
            using var rig = new HumanoidTestRig(gait);
            rig.Initialize();
            rig.Step(0f);
            var leftFoot = rig.LeftAnkle.position;
            var rightFoot = rig.RightAnkle.position;
            float low = 0f, high = 0f, slip = 0f, headRoll = 0f;
            for (var time = 0f; time < gait.IdleShiftPeriod; time += Dt)
            {
                rig.Step(0f);
                low = Mathf.Min(low, rig.Hips.localPosition.x);
                high = Mathf.Max(high, rig.Hips.localPosition.x);
                slip = Mathf.Max(slip, Vector3.Distance(leftFoot, rig.LeftAnkle.position), Vector3.Distance(rightFoot, rig.RightAnkle.position));
                headRoll = Mathf.Max(headRoll, Mathf.Abs(Mathf.Asin((rig.Neck.rotation * Vector3.right).y) * Mathf.Rad2Deg));
            }
            Assert.That(high, Is.EqualTo(gait.IdleShift).Within(0.004f), "over the right leg");
            Assert.That(low, Is.EqualTo(-gait.IdleShift).Within(0.004f), "over the left leg");
            Assert.That(slip, Is.LessThan(0.004f), "feet stay where they stand");
            Assert.That(headRoll, Is.LessThan(0.5f), "head stays level");
        }

        [Test]
        public void WalkingLeavesNoIdleShift()
        {
            var plain = ScriptableObject.CreateInstance<GaitProfile>();
            plain.IdleShift = 0f;
            plain.IdleShiftRoll = 0f;
            using var shifting = new HumanoidTestRig(ScriptableObject.CreateInstance<GaitProfile>());
            using var steady = new HumanoidTestRig(plain);
            shifting.Initialize();
            steady.Initialize();
            for (var frame = 0; frame < 150; frame++)
            {
                shifting.Step(1.5f);
                steady.Step(1.5f);
            }
            Assert.That(shifting.Sample(), Is.EqualTo(steady.Sample()).Within(1e-5f));
        }

        [Test]
        public void AShamblerTipsOverTheLegItStandsOn()
        {
            using var rig = new HumanoidTestRig(GaitProfile.CreateShambler());
            rig.Initialize();
            float overLeft = 0f, overRight = 0f;
            int leftFrames = 0, rightFrames = 0;
            for (var time = 0f; time < 6f; time += Dt)
            {
                rig.Step(1.2f);
                if (time < 1f) continue;
                var roll = Mathf.DeltaAngle(0f, rig.Spine.localEulerAngles.z);
                if (rig.Animator.LeftFootPlanted && !rig.Animator.RightFootPlanted)
                {
                    overLeft += roll;
                    leftFrames++;
                }
                else if (rig.Animator.RightFootPlanted && !rig.Animator.LeftFootPlanted)
                {
                    overRight += roll;
                    rightFrames++;
                }
            }
            Assert.That(overLeft / leftFrames, Is.GreaterThan(2f), "leans left on the left leg");
            Assert.That(overRight / rightFrames, Is.LessThan(-2f), "leans right on the right leg");
        }

        [Test]
        public void AReachingHandLeavesTheOtherArmDown()
        {
            using var rig = new HumanoidTestRig(ScriptableObject.CreateInstance<GaitProfile>());
            rig.Initialize();
            var pose = PoseModifiers.Neutral;
            pose.Reach = 1f;
            rig.Motion.Pose = pose;
            for (var time = 0f; time < 0.45f; time += Dt) rig.Step(0f);
            Assert.That(ShoulderPitch(rig.RightShoulder), Is.LessThan(-55f), "right hand out within the reach time");
            Assert.That(ShoulderPitch(rig.LeftShoulder), Is.GreaterThan(-10f), "left arm stays down");
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void ArmBiasDroopsOneOutstretchedArm(float bias)
        {
            using var rig = new HumanoidTestRig(GaitProfile.CreateShambler());
            rig.Initialize();
            rig.Animator.ArmBias = bias;
            rig.Motion.Pose = new PoseModifiers { ArmsForward = 0.8f, LookWeight = 1f };
            for (var frame = 0; frame < 120; frame++) rig.Step(1.2f);
            var drooping = bias > 0f ? rig.LeftShoulder : rig.RightShoulder;
            var level = bias > 0f ? rig.RightShoulder : rig.LeftShoulder;
            Assert.That(ShoulderPitch(drooping) - ShoulderPitch(level), Is.EqualTo(18f).Within(3f));
        }
    }
}

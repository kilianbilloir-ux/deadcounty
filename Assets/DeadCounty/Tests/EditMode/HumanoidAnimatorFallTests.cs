using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;

namespace DeadCounty.Tests
{
    // Knocked down and back up, finished on the ground, and the player's blows aimed at a body lying there.
    public class HumanoidAnimatorFallTests
    {
        const float Dt = HumanoidTestRig.Dt;

        static HumanoidTestRig Standing()
        {
            var rig = new HumanoidTestRig(ScriptableObject.CreateInstance<GaitProfile>());
            rig.Initialize();
            Run(rig, 0.5f);
            return rig;
        }

        static void Run(HumanoidTestRig rig, float seconds)
        {
            for (var time = 0f; time < seconds; time += Dt) rig.Step(0f);
        }

        static HumanoidTestRig Felled()
        {
            var rig = Standing();
            rig.Animator.KnockDown(Vector3.back);
            Run(rig, 0.6f);
            return rig;
        }

        [Test]
        public void AFelledBodyLiesClawingAtTheGroundThenGetsBackUp()
        {
            using var rig = Felled();
            Assert.That(rig.Animator.Down, Is.True);
            Assert.That(rig.Animator.Collapsed, Is.False, "a felled body counts as a corpse");
            Assert.That(rig.Animator.enabled, Is.True);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.back), Is.LessThan(1f), "the body does not lie away from the blow");
            var shoulder = rig.LeftShoulder.localRotation;
            Run(rig, 0.25f);
            Assert.That(Quaternion.Angle(shoulder, rig.LeftShoulder.localRotation), Is.GreaterThan(2f), "the felled body lies as still as a corpse");

            rig.Animator.StandUp(1f);
            Run(rig, 0.5f);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.up), Is.InRange(10f, 80f), "the body did not rise gradually");
            Run(rig, 0.55f);
            Assert.That(rig.Animator.Down, Is.False);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.up), Is.LessThan(1f), "the body did not stand back up");
            Assert.That(rig.Model.localPosition.y, Is.EqualTo(0f).Within(1e-4f), "the body stands above the ground");
        }

        [Test]
        public void FelledAgainWhileGettingUpTheBodyFallsBackTheWayItCame()
        {
            using var rig = Felled();
            rig.Animator.StandUp(1f);
            Run(rig, 0.5f);
            rig.Animator.KnockDown(Vector3.forward);
            Run(rig, 0.6f);
            Assert.That(rig.Animator.Down, Is.True);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.back), Is.LessThan(1f));
        }

        [Test]
        public void KilledOnTheGroundTheBodyStaysDownAndStopsAnimating()
        {
            using var rig = Felled();
            rig.Animator.Collapse(Vector3.forward);
            rig.Step(0f);
            Assert.That(rig.Animator.Collapsed, Is.True);
            Assert.That(rig.Animator.enabled, Is.False, "the corpse keeps animating");
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.back), Is.LessThan(1f), "the corpse turned over");
        }

        [Test]
        public void KilledWhileGettingUpTheBodyFallsBackDown()
        {
            using var rig = Felled();
            rig.Animator.StandUp(1f);
            Run(rig, 0.5f);
            rig.Animator.Collapse(Vector3.forward);
            Run(rig, 0.6f);
            Assert.That(rig.Animator.Collapsed, Is.True);
            Assert.That(rig.Animator.enabled, Is.False);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.back), Is.LessThan(1f));
        }

        [Test]
        public void SnapStandsAFelledBodyBackUpAtOnce()
        {
            using var rig = Felled();
            rig.Animator.Snap();
            rig.Step(0f);
            Assert.That(rig.Animator.Down, Is.False);
            Assert.That(Vector3.Angle(rig.Model.up, Vector3.up), Is.LessThan(1f));
        }

        [Test]
        public void AStompLiftsTheRightKneeThenStampsTheFootDownInFront()
        {
            using var rig = Standing();
            var rest = rig.RightAnkle.position;
            var otherRest = rig.LeftAnkle.position;
            var pose = PoseModifiers.Neutral;

            pose.Stomp = 0.35f;
            rig.Motion.Pose = pose;
            rig.Step(0f);
            Assert.That(rig.RightAnkle.position.y - rest.y, Is.GreaterThan(0.25f), "the knee did not lift the foot");

            // Held there, the hips have time to dip as they do through a real wind-up.
            pose.Stomp = 0.5f;
            rig.Motion.Pose = pose;
            Run(rig, 0.3f);
            Assert.That(rig.RightAnkle.position.y - rest.y, Is.EqualTo(0f).Within(0.02f), "the foot did not reach the ground");
            Assert.That(rig.RightAnkle.position.z - rest.z, Is.GreaterThan(0.2f), "the foot did not land in front");

            pose.Stomp = 1f;
            rig.Motion.Pose = pose;
            rig.Step(0f);
            Assert.That(Vector3.Distance(rig.RightAnkle.position, rest), Is.LessThan(0.02f), "the foot did not come back home");
            Assert.That(Vector3.Distance(rig.LeftAnkle.position, otherRest), Is.LessThan(0.02f), "the standing foot moved");
        }

        [Test]
        public void AChopRaisesTheArmOverheadThenBringsItDownInFront()
        {
            using var rig = Standing();
            var pose = PoseModifiers.Neutral;
            pose.SwingWeight = 1f;
            pose.Chop = 1f;
            rig.Motion.Pose = pose;
            rig.Step(0f);
            var cocked = rig.RightElbow.position - rig.RightShoulder.position;
            Assert.That(cocked.y, Is.GreaterThan(0.2f), "the arm is not raised overhead");

            pose.Swing = 1f;
            rig.Motion.Pose = pose;
            rig.Step(0f);
            var followed = rig.RightElbow.position - rig.RightShoulder.position;
            Assert.That(followed.y, Is.LessThan(-0.1f), "the arm did not come down");
            Assert.That(followed.z, Is.GreaterThan(0.05f), "the arm did not end in front");
            Assert.That(Mathf.Abs(followed.x), Is.LessThan(0.12f), "the chop swept across the body");
        }
    }
}

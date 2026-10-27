#if UNITY_EDITOR
using System.Collections;
using DeadCounty.Animation;
using DeadCounty.Core;
using DeadCounty.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Body language the pose driver adds on the generated rig: recoil, breathlessness, limp.
    public class PlayerPoseDriverTests : PlayerRigTests
    {
        ThirdPersonController controller;

        [UnitySetUp]
        public IEnumerator SetUpDriver()
        {
            controller = Rig.GetComponentInChildren<ThirdPersonController>();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AHitFromTheFrontRecoilsBackwardThenSettles()
        {
            var player = Health.transform;
            Health.ApplyHit(10f, player.position + player.forward * 1.5f);
            yield return null;
            Assert.That(controller.Pose.FlinchPitch, Is.LessThan(-10f), "a hit from the front must throw the torso back");
            Assert.That(Mathf.Abs(controller.Pose.FlinchRoll), Is.LessThan(2f));

            yield return new WaitForSeconds(0.5f);
            Assert.That(controller.Pose.FlinchPitch, Is.Zero, "the recoil must settle");
        }

        [UnityTest]
        public IEnumerator AHitFromTheRightLeansTheBodyLeft()
        {
            var player = Health.transform;
            Health.ApplyHit(10f, player.position + player.right * 1.5f);
            yield return null;
            Assert.That(controller.Pose.FlinchRoll, Is.GreaterThan(10f));
        }

        [UnityTest]
        public IEnumerator SpentStaminaPantsAndAQuarterOfHealthLimps()
        {
            var animator = Rig.GetComponentInChildren<ProceduralHumanoidAnimator>();
            Stamina.Model.Tick(StaminaActivity.Sprint, 10f);
            yield return null;
            Assert.That(Stamina.Model.Exhausted);
            Assert.That(controller.Pose.Pant, Is.EqualTo(1f));
            Assert.That(animator.Limp.Enabled, Is.False);

            Health.Pool.Apply(80f);
            yield return null;
            Assert.That(animator.Limp.Enabled, "under a quarter of health the player must limp");
            Health.Heal(50f);
            yield return null;
            Assert.That(animator.Limp.Enabled, Is.False);
        }
    }
}
#endif

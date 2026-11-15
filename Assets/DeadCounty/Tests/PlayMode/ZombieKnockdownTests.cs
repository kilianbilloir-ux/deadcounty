using System.Collections;
using DeadCounty.AI;
using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // A real zombie felled on the NavMesh: the fall, the body lying low, getting up, and a death on the ground.
    public class ZombieKnockdownTests : ZombieWorldTests
    {
        static readonly Vector3 Attacker = new(0f, 0f, 1.2f);

        IEnumerator FelledFromTheFront(ZombieAgent zombie)
        {
            yield return ZombieTestWorld.Settle(zombie);
            Assert.That(zombie.Health.ApplyDamage(10f, Attacker, 1f, 0.5f, true), Is.True);
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Down));
            Assert.That(zombie.IsDown, Is.True);
        }

        [UnityTest]
        public IEnumerator AZombieFelledFromTheFrontLiesOnItsBackThenGetsUpAndTurnsOnItsAttacker()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = Attacker;
            var zombie = World.SpawnZombie(Vector3.zero, withAnimator: true);
            var animator = zombie.GetComponent<ProceduralHumanoidAnimator>();
            var capsule = zombie.GetComponent<CapsuleCollider>();
            var model = zombie.transform.Find("Model");
            var falls = 0;
            zombie.Felled += () => falls++;
            yield return FelledFromTheFront(zombie);

            Assert.That(falls, Is.EqualTo(1));
            Assert.That(capsule.direction, Is.EqualTo(2), "the collider still stands");
            Assert.That(capsule.center.y, Is.LessThan(0.3f));
            Assert.That(capsule.center.z, Is.LessThan(0f), "the collider does not lie behind the zombie");
            yield return ZombieTestWorld.WaitUntil(() => Vector3.Angle(model.up, Vector3.back) < 1f, 1.5f);
            Assert.That(Vector3.Angle(model.up, Vector3.back), Is.LessThan(1f), "the body does not lie on its back away from the blow");
            Assert.That(zombie.Position.z, Is.EqualTo(-0.5f).Within(0.05f), "the blow did not push the body half a metre back");
            Assert.That(zombie.BodyCenter.z, Is.EqualTo(-0.5f - 0.85f).Within(0.05f), "blows do not aim at the torso on the ground");

            var tuning = World.Profile.Tuning;
            yield return ZombieTestWorld.WaitUntil(() => zombie.State != ZombieState.Down, tuning.DownTime * 2f + tuning.RiseTime);
            // Coroutines resume before LateUpdate: let the animator see this frame.
            yield return null;
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(zombie.IsDown, Is.False);
            Assert.That(animator.Down, Is.False, "the body is still getting up");
            Assert.That(Vector3.Angle(model.up, Vector3.up), Is.LessThan(8f));
            Assert.That(capsule.direction, Is.EqualTo(1), "the collider did not stand back up");
            Assert.That(capsule.center, Is.EqualTo(new Vector3(0f, 0.9f, 0f)));
            Assert.That(World.Saw(zombie, ZombieState.Down, out var fell), Is.True);
            Assert.That(fell.Reason, Is.EqualTo(TransitionReason.Felled));
            Assert.That(World.Saw(zombie, ZombieState.Chase, out var chase), Is.True);
            Assert.That(chase.Time - fell.Time, Is.EqualTo(zombie.Brain.DownDuration + tuning.RiseTime).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator KilledOnTheGroundAZombieStaysDownUntilResetToSpawn()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = Attacker;
            var zombie = World.SpawnZombie(Vector3.zero, withAnimator: true);
            var animator = zombie.GetComponent<ProceduralHumanoidAnimator>();
            var capsule = zombie.GetComponent<CapsuleCollider>();
            var model = zombie.transform.Find("Model");
            yield return FelledFromTheFront(zombie);
            yield return ZombieTestWorld.WaitUntil(() => Vector3.Angle(model.up, Vector3.back) < 1f, 1.5f);

            Assert.That(zombie.Health.ApplyDamage(1000f, Attacker, 0f, 0f, true), Is.True);
            yield return ZombieTestWorld.WaitUntil(() => animator.Collapsed, 1f);
            Assert.That(animator.Collapsed, Is.True);
            Assert.That(animator.enabled, Is.False, "the corpse kept animating");
            Assert.That(capsule.enabled, Is.False, "the corpse still blocks the way");
            Assert.That(Vector3.Angle(model.up, Vector3.back), Is.LessThan(1f), "the corpse turned over");

            zombie.ResetToSpawn();
            yield return null;
            Assert.That(zombie.IsDown, Is.False);
            Assert.That(capsule.enabled, Is.True);
            Assert.That(capsule.direction, Is.EqualTo(1));
            Assert.That(Vector3.Angle(model.up, Vector3.up), Is.LessThan(8f), "the revived zombie is still lying down");
        }
    }
}

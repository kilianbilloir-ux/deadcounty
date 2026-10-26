using System.Collections;
using DeadCounty.AI;
using DeadCounty.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // A zombie walking on the NavMesh up to a standing target, through the whole attack, death and reset.
    public class ZombieAttackTests : ZombieWorldTests
    {
        [UnityTest]
        public IEnumerator ChasingZombieReachesTargetAndStrikesOnlyAfterTheWindup()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Position = new Vector3(0f, 0f, 5f);
            var zombie = World.SpawnZombie(Vector3.zero);
            var attack = World.Profile.Tuning.Attack;

            yield return ZombieTestWorld.WaitUntil(() => World.Target.HitCount > 0, 15f);

            Assert.That(World.Saw(zombie, ZombieState.Chase, out _), Is.True, "never chased");
            Assert.That(World.Saw(zombie, ZombieState.Attack, out var attackStart), Is.True, $"never attacked, still {zombie.State} at {zombie.Position}");
            Assert.That(World.Target.HitCount, Is.EqualTo(1), "the target was not hit exactly once");
            Assert.That(World.Target.FirstHitTime - attackStart.Time, Is.GreaterThanOrEqualTo(attack.Windup - 1e-3f), "struck before the end of the wind-up");
            var distance = ZombieTestWorld.PlanarDistance(zombie.Position, World.Target.Position);
            Assert.That(distance, Is.InRange(attack.ContactDistance - 0.05f, attack.HitReach), "the zombie did not end its lunge in reach");
            Assert.That(World.Strikes.Count, Is.EqualTo(1));
            Assert.That(World.Strikes[0].Hit, Is.True);
        }

        // The revive warps the body back to spawn: without the snap that warp would read as a
        // sprint and pitch the spine backward, so the test walks the zombie first to make it count.
        [UnityTest]
        public IEnumerator DeadZombieAnimatorStopsAndRestartsOnReset()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            var spawn = new Vector3(2f, 0f, 3f);
            var zombie = World.SpawnZombie(spawn, 90f, withAnimator: true);
            yield return ZombieTestWorld.Settle(zombie);
            var animator = zombie.GetComponent<ProceduralHumanoidAnimator>();
            Assert.That(animator, Is.Not.Null, "the test zombie has no animator");
            Assert.That(animator.enabled, Is.True);

            World.Target.Position = spawn + 8f * new Vector3(1f, 0f, 1f).normalized;
            World.Target.Visible = true;
            yield return ZombieTestWorld.WaitUntil(() => zombie.State == ZombieState.Chase, 6f);
            yield return ZombieTestWorld.WaitUntil(() => ZombieTestWorld.PlanarDistance(zombie.Position, spawn) >= 2.5f, 10f);
            Assert.That(ZombieTestWorld.PlanarDistance(zombie.Position, World.Target.Position), Is.GreaterThan(3f), "the zombie reached attack range before the kill");

            Assert.That(zombie.Health.ApplyDamage(1000f, Vector3.zero), Is.True);
            Assert.That(animator.enabled, Is.False, "the corpse kept animating");

            World.Target.Visible = false;
            var resets = zombie.MotionResetId;
            zombie.ResetToSpawn();
            yield return null;
            Assert.That(zombie.MotionResetId, Is.EqualTo(resets + 1), "the revive did not snap the animator");
            Assert.That(animator.enabled, Is.True, "the revived zombie never animates again");
            var spine = zombie.transform.Find("Model/Hips/Spine");
            for (var i = 0; i < 2; i++)
            {
                yield return null;
                Assert.That(SignedPitch(spine), Is.GreaterThan(0f), $"backward lean spike at frame {i}");
            }
        }

        static float SignedPitch(Transform joint)
        {
            var x = joint.localEulerAngles.x;
            return x > 180f ? x - 360f : x;
        }

        [UnityTest]
        public IEnumerator DeadZombieLeavesTheManagerUntilResetToSpawn()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, -15f);
            var spawn = new Vector3(2f, 0f, 3f);
            var zombie = World.SpawnZombie(spawn, 90f);
            yield return ZombieTestWorld.Settle(zombie);

            Assert.That(zombie.Health.ApplyDamage(1000f, Vector3.zero), Is.True);
            Assert.That(zombie.IsDead, Is.True);
            Assert.That(zombie.NavAgent.enabled, Is.False);
            Assert.That(World.Manager.Agents.Count, Is.Zero);
            Assert.That(World.Manager.Records[0].Dead, Is.True);
            yield return null;

            var resets = zombie.MotionResetId;
            zombie.ResetToSpawn();
            yield return null;

            Assert.That(zombie.IsDead, Is.False);
            Assert.That(zombie.Health.IsAlive, Is.True);
            Assert.That(zombie.NavAgent.enabled, Is.True);
            Assert.That(World.Manager.Agents.Count, Is.EqualTo(1));
            Assert.That(World.Manager.Records.Count, Is.EqualTo(1), "the revived zombie must reuse its record");
            Assert.That(World.Manager.Records[0].Dead, Is.False);
            Assert.That(zombie.MotionResetId, Is.EqualTo(resets + 1));
            Assert.That(ZombieTestWorld.PlanarDistance(zombie.Position, spawn), Is.LessThan(0.05f));
            Assert.That(Vector3.Angle(zombie.Heading, Vector3.right), Is.LessThan(1f));
        }
    }
}

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
        public IEnumerator DeadZombieFallsAwayFromTheBlowAndStandsBackUpOnReset()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            var spawn = new Vector3(2f, 0f, 3f);
            var zombie = World.SpawnZombie(spawn, 90f, withAnimator: true);
            yield return ZombieTestWorld.Settle(zombie);
            var animator = zombie.GetComponent<ProceduralHumanoidAnimator>();
            var body = zombie.GetComponent<Collider>();
            var model = zombie.transform.Find("Model");
            Assert.That(animator, Is.Not.Null, "the test zombie has no animator");
            Assert.That(animator.enabled, Is.True);

            World.Target.Position = spawn + 8f * new Vector3(1f, 0f, 1f).normalized;
            World.Target.Visible = true;
            yield return ZombieTestWorld.WaitUntil(() => zombie.State == ZombieState.Chase, 6f);
            yield return ZombieTestWorld.WaitUntil(() => ZombieTestWorld.PlanarDistance(zombie.Position, spawn) >= 2.5f, 10f);
            Assert.That(ZombieTestWorld.PlanarDistance(zombie.Position, World.Target.Position), Is.GreaterThan(3f), "the zombie reached attack range before the kill");

            var blow = zombie.Position + Vector3.forward;
            Assert.That(zombie.Health.ApplyDamage(1000f, blow), Is.True);
            Assert.That(body.enabled, Is.False, "the corpse still blocks the way");
            yield return ZombieTestWorld.WaitUntil(() => animator.Collapsed, 3f);
            Assert.That(animator.Collapsed, Is.True, "the corpse never reached the ground");
            Assert.That(Vector3.Angle(model.up, Vector3.back), Is.LessThan(2f), "the corpse does not lie away from the blow");
            Assert.That(animator.enabled, Is.False, "the corpse kept animating");

            World.Target.Visible = false;
            var resets = zombie.MotionResetId;
            zombie.ResetToSpawn();
            yield return null;
            Assert.That(zombie.MotionResetId, Is.EqualTo(resets + 1), "the revive did not snap the animator");
            Assert.That(animator.enabled, Is.True, "the revived zombie never animates again");
            Assert.That(body.enabled, Is.True);
            Assert.That(Vector3.Angle(model.up, Vector3.up), Is.LessThan(8f), "the revived zombie is still lying down");
            var spine = zombie.transform.Find("Model/Hips/Spine");
            for (var i = 0; i < 2; i++)
            {
                yield return null;
                Assert.That(SignedPitch(spine), Is.GreaterThan(0f), $"backward lean spike at frame {i}");
            }
        }

        [UnityTest]
        public IEnumerator AStruckZombieIsShovedBackThenTurnsOnItsAttacker()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, -1.2f);
            var zombie = World.SpawnZombie(Vector3.zero);
            yield return ZombieTestWorld.Settle(zombie);
            Assert.That(ZombieTestWorld.IsCalm(zombie.State), Is.True);

            Assert.That(zombie.Health.ApplyDamage(10f, World.Target.Position, 0.9f, 0.5f), Is.True);
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Stagger));
            yield return ZombieTestWorld.WaitUntil(() => zombie.State != ZombieState.Stagger, 3f);

            Assert.That(zombie.Position.z, Is.EqualTo(0.5f).Within(0.05f), "the blow did not push the zombie half a metre back");
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(World.Saw(zombie, ZombieState.Stagger, out var reel), Is.True);
            Assert.That(reel.Reason, Is.EqualTo(TransitionReason.Hit));
            Assert.That(World.Saw(zombie, ZombieState.Chase, out var chase), Is.True);
            Assert.That(chase.Time - reel.Time, Is.EqualTo(0.9f).Within(0.05f));
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

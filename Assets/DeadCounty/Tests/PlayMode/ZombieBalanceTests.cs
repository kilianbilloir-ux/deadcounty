using System.Collections;
using DeadCounty.AI;
using DeadCounty.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // The tuning checklist of the zombie plan, played out on the NavMesh: escaping, being surrounded, night sight.
    public class ZombieBalanceTests : ZombieWorldTests
    {
        // Same as PlayerHealth.invulnerabilityTime on the player rig.
        const float PlayerInvulnerability = 0.8f;
        const float RunSpeed = 3.5f;

        // Plan: running from a zombie that chases from 6 m breaks its sight (20 m by day) in about 6 s.
        [UnityTest]
        public IEnumerator RunningAwayBreaksSightInAboutSixSeconds()
        {
            World = new ZombieTestWorld(4f, 12f);
            var zombie = World.SpawnZombie(new Vector3(0f, 0f, -45f));
            World.Target.Position = new Vector3(0f, 0f, -39f);
            yield return ZombieTestWorld.WaitUntil(() => zombie.State == ZombieState.Chase, 6f);
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Chase), "never chased");

            var start = Time.time;
            World.Target.Velocity = Vector3.forward * RunSpeed;
            while (zombie.Perception.TargetVisible && Time.time - start < 15f)
            {
                yield return null;
                World.Target.Position += World.Target.Velocity * Time.deltaTime;
            }
            var lostAfter = Time.time - start;
            TestContext.WriteLine($"sight broken after {lostAfter:0.00} s at {ZombieTestWorld.PlanarDistance(zombie.Position, World.Target.Position):0.0} m");
            Assert.That(lostAfter, Is.InRange(4.5f, 8f));
        }

        // Plan: surrounded by three zombies in the open, a player who does not move dies in about 3.6 s. The zombies
        // take turns, so each strike lands after the invulnerability of the previous one.
        [UnityTest]
        public IEnumerator ThreeZombiesKillAStandingPlayerInAboutFourSeconds()
        {
            World = new ZombieTestWorld(4f);
            World.Target.InvulnerabilityTime = PlayerInvulnerability;
            World.Target.Position = Vector3.zero;
            for (var i = 0; i < 3; i++)
            {
                var angle = i * 120f;
                var position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.2f;
                World.SpawnZombie(position, angle + 180f, 11 + i, deterministic: false);
            }
            yield return ZombieTestWorld.WaitUntil(() => !World.Target.IsAlive, 20f);

            var firstAttack = float.NaN;
            var previous = float.NegativeInfinity;
            foreach (var t in World.Transitions)
            {
                if (t.To != ZombieState.Attack) continue;
                if (float.IsNaN(firstAttack)) firstAttack = t.Time;
                Assert.That(t.Time - previous, Is.GreaterThanOrEqualTo(World.Profile.Tuning.Attack.TurnInterval - 1e-3f), $"wind-up at {t.Time:0.00} s too close to the previous one");
                previous = t.Time;
            }
            Assert.That(World.Target.IsAlive, Is.False, $"survived {World.Target.AcceptedHits} hits of {World.Target.HitCount} strikes");
            var timeToKill = World.Target.DeathTime - firstAttack;
            TestContext.WriteLine($"dead {timeToKill:0.00} s after the first wind-up, {World.Target.HitCount} strikes");
            Assert.That(timeToKill, Is.InRange(2.5f, 4.5f));
        }

        // Plan: at night a standing player is seen at 8 m.
        [UnityTest]
        public IEnumerator AtNightAStandingPlayerIsSeenAt7MetresButNotAt9()
        {
            World = new ZombieTestWorld(4f);
            WorldLighting.NightFactor = 1f;
            var near = World.SpawnZombie(new Vector3(-10f, 0f, 0f), 0f, 1);
            var far = World.SpawnZombie(new Vector3(10f, 0f, 0f), 0f, 2);
            World.Target.Visible = false;
            yield return ZombieTestWorld.Settle(near);
            yield return ZombieTestWorld.Settle(far);

            // One target, two zombies at different distances: 7 m in front of one, 9 m in front of the other.
            World.Target.Position = new Vector3(-10f, 0f, 7f);
            World.Target.Visible = true;
            yield return ZombieTestWorld.WaitUntil(() => World.Saw(near, ZombieState.Alert, out _), 4f);
            Assert.That(World.Saw(near, ZombieState.Alert, out _), Is.True, "not seen at 7 m at night");

            World.Target.Position = new Vector3(10f, 0f, 9f);
            yield return ZombieTestWorld.WaitUntil(() => World.Saw(far, ZombieState.Alert, out _), 4f);
            Assert.That(World.Saw(far, ZombieState.Alert, out _), Is.False, "seen at 9 m at night");
        }
    }
}

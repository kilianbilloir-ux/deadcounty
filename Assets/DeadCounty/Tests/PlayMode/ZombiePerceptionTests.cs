using System.Collections;
using DeadCounty.AI;
using DeadCounty.Core;
using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeadCounty.Tests
{
    // Sight and hearing through real physics queries: a zombie at the origin facing +Z.
    public class ZombiePerceptionTests : ZombieWorldTests
    {
        const float SprintNoise = 12f;
        const int NoiseSource = 424242;

        [UnityTest]
        public IEnumerator SprintNoiseEightMetresAwayIsHeard()
        {
            var zombie = SpawnListener();
            yield return ZombieTestWorld.Settle(zombie);

            NoiseSystem.Emit(new Vector3(8f, 0f, 0f), SprintNoise, NoiseKind.Footstep, NoiseSource);
            yield return ZombieTestWorld.WaitUntil(() => !ZombieTestWorld.IsCalm(zombie.State), 0.5f);

            Assert.That(ZombieTestWorld.IsCalm(zombie.State), Is.False, $"still {zombie.State} 0.5 s after the noise");
            Assert.That(zombie.LastNoiseWalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SprintNoiseBehindAWallIsNotHeard()
        {
            var zombie = SpawnListener();
            World.Box(new Vector3(4f, 1.5f, 0f), new Vector3(0.3f, 3f, 6f), GameLayers.WallTag);
            yield return ZombieTestWorld.Settle(zombie);
            var senses = zombie.SenseCount;

            // One wall halves the 12 m reach to 6 m, short of the 8 m to the zombie.
            NoiseSystem.Emit(new Vector3(8f, 0f, 0f), SprintNoise, NoiseKind.Footstep, NoiseSource);
            yield return ZombieTestWorld.WaitUntil(() => !ZombieTestWorld.IsCalm(zombie.State), 1.5f);

            Assert.That(zombie.SenseCount, Is.GreaterThan(senses + 5), "the zombie did not sense while waiting");
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Idle));
        }

        [UnityTest]
        public IEnumerator VisibleTargetIsAlertedThenChasedAfterTheTelegraph()
        {
            var zombie = SpawnWatcher();
            yield return ZombieTestWorld.Settle(zombie);
            World.Target.Visible = true;

            yield return ZombieTestWorld.WaitUntil(() => World.Saw(zombie, ZombieState.Chase, out _), 6f);

            Assert.That(World.Saw(zombie, ZombieState.Alert, out var alert), Is.True, "never alerted");
            Assert.That(alert.Reason, Is.EqualTo(TransitionReason.SawTarget));
            Assert.That(World.Saw(zombie, ZombieState.Chase, out var chase), Is.True, $"never chased, still {zombie.State}");
            Assert.That(chase.Time - alert.Time, Is.GreaterThanOrEqualTo(1.5f - 1e-3f), "the chase cut the alert telegraph short");
        }

        [UnityTest]
        public IEnumerator TargetBehindAWallIsNeverSeen()
        {
            var zombie = SpawnWatcher();
            World.Box(new Vector3(0f, 1.5f, 4f), new Vector3(6f, 3f, 0.3f), GameLayers.WallTag);
            yield return ZombieTestWorld.Settle(zombie);
            World.Target.Visible = true;
            var senses = zombie.SenseCount;

            yield return ZombieTestWorld.WaitUntil(() => World.Saw(zombie, ZombieState.Alert, out _), 3f);

            Assert.That(zombie.SenseCount, Is.GreaterThan(senses + 10), "the zombie did not sense while waiting");
            Assert.That(World.Saw(zombie, ZombieState.Alert, out _), Is.False, "alerted through a wall");
            Assert.That(zombie.Perception.TargetVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator TargetBehindGlassIsSeen()
        {
            var zombie = SpawnWatcher();
            World.Box(new Vector3(0f, 1.5f, 4f), new Vector3(6f, 3f, 0.3f), GameLayers.GlassTag);
            yield return ZombieTestWorld.Settle(zombie);
            World.Target.Visible = true;

            yield return ZombieTestWorld.WaitUntil(() => World.Saw(zombie, ZombieState.Alert, out _), 3f);

            Assert.That(World.Saw(zombie, ZombieState.Alert, out var alert), Is.True, "not seen through glass");
            Assert.That(alert.Reason, Is.EqualTo(TransitionReason.SawTarget));
        }

        [UnityTest]
        public IEnumerator WeakerNoiseInLaterSenseStillCounts()
        {
            var zombie = SpawnListener();
            yield return ZombieTestWorld.Settle(zombie);

            var t = Time.time;
            NoiseSystem.Emit(new Vector3(8f, 0f, 0f), SprintNoise, NoiseKind.Footstep, NoiseSource);
            zombie.Sense(World.Target, t);
            NoiseSystem.Emit(new Vector3(4f, 0f, 0f), 5f, NoiseKind.Footstep, NoiseSource);
            zombie.Sense(World.Target, t + 0.05f);
            zombie.Think(ZombieTestWorld.FrameTime);

            Assert.That(zombie.Perception.HeardNoise, Is.True);
            Assert.That(zombie.Perception.NoisePosition.x, Is.EqualTo(4f).Within(1e-3f));
        }

        // The target stands behind the zombie and is invisible, so only the noise can reach it.
        ZombieAgent SpawnListener()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, -15f);
            return World.SpawnZombie(Vector3.zero);
        }

        // The target stands still 8 m in front, hidden until the zombie has settled.
        ZombieAgent SpawnWatcher()
        {
            World = new ZombieTestWorld(4f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, 8f);
            return World.SpawnZombie(Vector3.zero);
        }
    }
}

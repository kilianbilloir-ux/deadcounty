using System.Collections;
using System.Collections.Generic;
using DeadCounty.AI;
using DeadCounty.Core;
using DeadCounty.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace DeadCounty.Tests
{
    // Time slicing and the active budget, at timeScale 1 so a game second is exactly 60 frames.
    public class ZombieManagerTests : ZombieWorldTests
    {
        const float NearSenseRate = 10f;

        [UnityTest]
        public IEnumerator ThirtyNearZombiesSenseAtTenHertzWithinTheFrameCap()
        {
            var zombies = SpawnGrid(30);
            yield return ZombieTestWorld.WaitUntil(() => false, 0.5f);
            var manager = World.Manager;
            Assert.That(manager.Agents.Count, Is.EqualTo(30));
            Assert.That(manager.ActiveCount, Is.EqualTo(30));
            foreach (var zombie in zombies) Assert.That(zombie.Lod, Is.EqualTo(ZombieLod.Near), zombie.name);

            var before = SenseCounts(zombies);
            var totalBefore = manager.TotalSenses;
            var maxPerFrame = 0;
            var end = Time.time + 1f;
            while (Time.time < end)
            {
                yield return null;
                maxPerFrame = Mathf.Max(maxPerFrame, manager.SensesThisFrame);
            }

            Assert.That(maxPerFrame, Is.LessThanOrEqualTo(manager.MaxSensesPerFrame));
            Assert.That(manager.TotalSenses - totalBefore, Is.InRange(30 * NearSenseRate * 0.7f, 30 * NearSenseRate * 1.3f));
            for (var i = 0; i < zombies.Count; i++)
                Assert.That(zombies[i].SenseCount - before[i], Is.InRange(NearSenseRate * 0.7f, NearSenseRate * 1.3f), zombies[i].name);
        }

        [UnityTest]
        public IEnumerator ZombiesBeyondTheBudgetGoDormantFarthestFirst()
        {
            var zombies = SpawnGrid(30);
            var far = new[] { World.SpawnZombie(new Vector3(-6f, 0f, 15f), 0f, 100), World.SpawnZombie(new Vector3(6f, 0f, 15f), 0f, 101) };
            yield return ZombieTestWorld.WaitUntil(() => false, 0.5f);
            var manager = World.Manager;

            Assert.That(manager.Agents.Count, Is.EqualTo(32));
            Assert.That(manager.ActiveCount, Is.EqualTo(manager.ActiveBudget));
            foreach (var zombie in zombies) Assert.That(zombie.Lod, Is.Not.EqualTo(ZombieLod.Dormant), zombie.name);
            var farSenses = new int[far.Length];
            for (var i = 0; i < far.Length; i++)
            {
                Assert.That(far[i].Lod, Is.EqualTo(ZombieLod.Dormant), far[i].name);
                farSenses[i] = far[i].SenseCount;
            }

            yield return ZombieTestWorld.WaitUntil(() => false, 1f);

            for (var i = 0; i < far.Length; i++) Assert.That(far[i].SenseCount, Is.EqualTo(farSenses[i]), $"{far[i].name} sensed while dormant");
            // Dormant from their first frame, the far two never thought their way from Wander into Idle.
            Assert.That(manager.CountIn(ZombieState.Idle), Is.EqualTo(30));
            Assert.That(manager.CountIn(ZombieState.Wander), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SenseThinkAndMoveDoNotAllocate()
        {
            World = new ZombieTestWorld(1f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, 10f);
            World.Box(new Vector3(4f, 1.5f, 0f), new Vector3(0.3f, 3f, 6f), GameLayers.WallTag);
            var zombie = World.SpawnZombie(Vector3.zero);
            yield return ZombieTestWorld.Settle(zombie);
            World.Target.Visible = true;
            World.Transitions.Capacity = 256;
            World.Strikes.Capacity = 256;

            // One second of sight rays and walls counted for every noise; the frame never ends, so the path stays
            // pending and the chase stands still.
            void Run()
            {
                for (var i = 0; i < 60; i++)
                {
                    NoiseSystem.Emit(new Vector3(8f, 0f, 0f), 12f, NoiseKind.Footstep, 424242);
                    zombie.Sense(World.Target, Time.time);
                    zombie.Think(ZombieTestWorld.FrameTime);
                    zombie.Move(ZombieTestWorld.FrameTime);
                }
            }

            // Alert after about 0.5 s and chase after 2 s: the measured third second runs code already JIT-compiled.
            Run();
            Run();
            Run();
            Assert.That(zombie.State, Is.EqualTo(ZombieState.Chase));
            Assert.That(Run, Is.Not.AllocatingGCMemory());
            Assert.That(zombie.LastNoiseWalls, Is.EqualTo(1));
            Assert.That(ZombieTestWorld.IsCalm(zombie.State), Is.False);
        }

        // A 6 x 5 grid of zombies 2 m apart, at most 22 m from the invisible target.
        List<ZombieAgent> SpawnGrid(int count)
        {
            World = new ZombieTestWorld(1f);
            World.Target.Visible = false;
            World.Target.Position = new Vector3(0f, 0f, -12f);
            var zombies = new List<ZombieAgent>(count);
            for (var i = 0; i < count; i++)
                zombies.Add(World.SpawnZombie(new Vector3(-5f + 2f * (i % 6), 0f, 2f * (i / 6)), 0f, i + 1));
            return zombies;
        }

        static int[] SenseCounts(List<ZombieAgent> zombies)
        {
            var counts = new int[zombies.Count];
            for (var i = 0; i < counts.Length; i++) counts[i] = zombies[i].SenseCount;
            return counts;
        }
    }
}
